using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EdhDeckBuilder.Agent.Analysis;

public sealed class DeckUpgrader(
    ISuggestionSource suggestionSource,
    ILlmClientFactory llmFactory,
    ILlmClassifier classifier,
    ILogger<DeckUpgrader> logger) : IDeckUpgrader
{
    private const int TopGapCount   = 5;
    private const int SuggestionCount = 3;

    public UsageTracker? UsageTracker { get; set; }

    public async Task<DeckUpgradeResult> UpgradeAsync(
        DeckAnalysisResult analysis,
        string? userFeedback,
        decimal? maxCardPriceUsd,
        Func<string, Task>? progress = null,
        CancellationToken ct = default)
    {
        await Report(progress, "Gathering upgrade candidates");

        // 1. Build candidate pool — EDHREC per commander, merged and de-duped
        var pool = await GatherPoolAsync(analysis.Commanders, ct);

        // 2. Existing oracle IDs — exclude anything already in the deck
        var existingIds = analysis.CommanderCards.Select(c => c.Card.OracleId)
            .Concat(analysis.Cards.Select(c => c.Card.OracleId))
            .ToHashSet();

        // 3. Color identity constraint
        var commanderIdentity = analysis.Commanders.Aggregate(Color.None, (ci, c) => ci | c.ColorIdentity);

        // 4. Filter pool: not already in deck, correct color, within budget
        var filtered = pool
            .Where(c =>
                !existingIds.Contains(c.Card.OracleId) &&
                (commanderIdentity == Color.None || c.Card.ColorIdentity.IsWithin(commanderIdentity)) &&
                (maxCardPriceUsd is null || c.Card.PriceUsd is null || c.Card.PriceUsd <= maxCardPriceUsd))
            .GroupBy(c => c.Card.OracleId)
            .Select(g => g.OrderByDescending(c => c.Inclusion).First())
            .ToList();

        logger.LogInformation("DeckUpgrade_Pool: {Total} raw, {Filtered} after filter", pool.Count, filtered.Count);

        if (filtered.Count == 0)
        {
            logger.LogWarning("DeckUpgrade: empty pool after filtering — returning no suggestions");
            return new DeckUpgradeResult { RoleUpgrades = [] };
        }

        // 5. Classify candidates so we know their role
        await Report(progress, "Classifying upgrade candidates");
        var classified = await classifier.ClassifyAsync(filtered, analysis.Commanders, ct);
        if (UsageTracker != null && classifier is IUsageTrackerAware aware)
            aware.SetUsageTracker(UsageTracker);

        var classifiedById = classified.ToDictionary(r => r.OracleId);

        // 6. Prioritize gaps
        var gaps = analysis.RoleGaps.Take(TopGapCount).ToList();
        if (gaps.Count == 0)
            return new DeckUpgradeResult { RoleUpgrades = [] };

        var prioritizedGaps = await PrioritizeGapsAsync(gaps, userFeedback, analysis.Commanders, ct);

        // 7. Select upgrades per gap
        var roleUpgrades = new List<RoleUpgrade>();
        var usedCutIds   = new HashSet<Guid>();

        foreach (var gap in prioritizedGaps)
        {
            await Report(progress, $"Finding upgrades for {RoleLabel(gap.Role)}");

            // Candidates for this role (primary match or secondary with reasonable weight)
            var roleCandidates = filtered
                .Where(c =>
                {
                    if (!classifiedById.TryGetValue(c.Card.OracleId, out var cr)) return false;
                    return cr.PrimaryRole == gap.Role ||
                           cr.Secondary.Any(s => s.Role == gap.Role && s.Weight >= 0.4);
                })
                .OrderByDescending(c => c.Inclusion)
                .Take(40)
                .ToList();

            if (roleCandidates.Count == 0)
            {
                logger.LogInformation("DeckUpgrade: no candidates for {Role}", gap.Role);
                continue;
            }

            // Cut pool: deck cards minus commanders, minus already proposed cuts
            var cutPool = analysis.Cards
                .Where(c => !usedCutIds.Contains(c.Card.OracleId))
                .ToList();

            if (cutPool.Count == 0) break;

            var suggestions = await SelectUpgradesAsync(
                gap, roleCandidates, cutPool, analysis, userFeedback, maxCardPriceUsd, ct);

            if (suggestions.Count == 0) continue;

            // Track cut IDs to avoid reusing them
            foreach (var s in suggestions)
                if (s.CutCard is not null)
                    usedCutIds.Add(s.CutCard.OracleId);

            roleUpgrades.Add(new RoleUpgrade { Gap = gap, Suggestions = suggestions });
        }

        logger.LogInformation("DeckUpgrade_Complete: {Gaps} gaps addressed", roleUpgrades.Count);
        return new DeckUpgradeResult { RoleUpgrades = roleUpgrades };
    }

    // ── Pool gathering ─────────────────────────────────────────────────────

    private async Task<List<CardCandidate>> GatherPoolAsync(
        IReadOnlyList<Card> commanders,
        CancellationToken ct)
    {
        var all = new List<CardCandidate>();
        foreach (var commander in commanders)
        {
            try
            {
                var recs = await suggestionSource.GetRecommendationsAsync(commander, ct);
                all.AddRange(recs);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DeckUpgrade: failed to fetch EDHREC pool for {Commander}", commander.Name);
            }
        }
        return all;
    }

    // ── Gap prioritization ─────────────────────────────────────────────────

    private async Task<IReadOnlyList<RoleGap>> PrioritizeGapsAsync(
        IReadOnlyList<RoleGap> gaps,
        string? userFeedback,
        IReadOnlyList<Card> commanders,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userFeedback))
            return gaps; // already ordered by shortfall

        try
        {
            var client  = llmFactory.CreateForCurrentUser();
            var model   = llmFactory.ClassificationModel; // Haiku — cheap, fast
            var message = UpgradeSelectionPrompt.FormatPrioritizationMessage(gaps, userFeedback, commanders);

            var request = new LlmRequest
            {
                Model          = model,
                MaxTokens      = 512,
                Temperature    = 0.1,
                SystemPrompt   = UpgradeSelectionPrompt.PrioritizationSystemPrompt,
                Messages       = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = message }] }],
                Tools          = [UpgradeSelectionPrompt.PrioritizationToolDefinition],
                ForcedToolName = UpgradeSelectionPrompt.PrioritizationToolName,
                EnableCaching  = false,
            };

            var response = await client.SendAsync(request, ct);
            RecordUsage("PrioritizeGaps", model, response);

            var toolUse = response.Content.OfType<LlmToolUseBlock>().FirstOrDefault();
            if (toolUse is null) return gaps;

            var rolesNode = toolUse.Input["prioritized_roles"];
            if (rolesNode is null) return gaps;

            var roleNames = rolesNode.Deserialize<List<string>>() ?? [];
            var gapsByRole = gaps.ToDictionary(g => g.Role);
            var ordered = roleNames
                .Select(name => Enum.TryParse<CardRole>(name, ignoreCase: true, out var r) ? r : (CardRole?)null)
                .Where(r => r.HasValue && gapsByRole.ContainsKey(r!.Value))
                .Select(r => gapsByRole[r!.Value])
                .ToList();

            // Append any gaps the model omitted (shouldn't happen but be safe)
            var included = ordered.Select(g => g.Role).ToHashSet();
            ordered.AddRange(gaps.Where(g => !included.Contains(g.Role)));

            return ordered;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DeckUpgrade: gap prioritization failed, falling back to shortfall order");
            return gaps;
        }
    }

    // ── Upgrade selection ──────────────────────────────────────────────────

    private async Task<IReadOnlyList<UpgradeSuggestion>> SelectUpgradesAsync(
        RoleGap gap,
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<AnalyzedCard> cutPool,
        DeckAnalysisResult analysis,
        string? userFeedback,
        decimal? maxCardPriceUsd,
        CancellationToken ct)
    {
        try
        {
            var client  = llmFactory.CreateForCurrentUser();
            var model   = llmFactory.SelectedModel;
            var message = UpgradeSelectionPrompt.FormatSelectionMessage(
                gap, candidates, cutPool, analysis.ActualCoverage,
                analysis.Commanders, userFeedback, maxCardPriceUsd);

            var request = new LlmRequest
            {
                Model          = model,
                MaxTokens      = 2048,
                Temperature    = 0.4,
                SystemPrompt   = UpgradeSelectionPrompt.SelectionSystemPrompt,
                Messages       = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = message }] }],
                Tools          = [UpgradeSelectionPrompt.SelectionToolDefinition],
                ForcedToolName = UpgradeSelectionPrompt.SelectionToolName,
                EnableCaching  = true,
            };

            var response = await client.SendAsync(request, ct);
            RecordUsage($"SelectUpgrades_{gap.Role}", model, response);

            var toolUse = response.Content.OfType<LlmToolUseBlock>().FirstOrDefault();
            if (toolUse is null) return [];

            var suggestionsNode = toolUse.Input["suggestions"];
            if (suggestionsNode is null) return [];

            var dtos = suggestionsNode.Deserialize<List<UpgradeSuggestionDto>>() ?? [];

            // Whitelists
            var addByOracle  = candidates.ToDictionary(c => c.Card.OracleId, c => c.Card);
            var cutByOracle  = cutPool.ToDictionary(c => c.Card.OracleId, c => c.Card);

            var results = new List<UpgradeSuggestion>(SuggestionCount);
            foreach (var dto in dtos.Take(SuggestionCount))
            {
                if (!Guid.TryParse(dto.AddOracleId, out var addId) || !addByOracle.TryGetValue(addId, out var addCard))
                {
                    logger.LogWarning("DeckUpgrade: unknown add_oracle_id {Id} for {Role}", dto.AddOracleId, gap.Role);
                    continue;
                }

                Card? cutCard = null;
                string? cutRationale = null;
                if (!string.IsNullOrEmpty(dto.CutOracleId) &&
                    Guid.TryParse(dto.CutOracleId, out var cutId) &&
                    cutByOracle.TryGetValue(cutId, out var foundCut))
                {
                    cutCard = foundCut;
                    cutRationale = dto.CutRationale;
                }
                else if (!string.IsNullOrEmpty(dto.CutOracleId))
                {
                    logger.LogWarning("DeckUpgrade: unknown cut_oracle_id {Id} — omitting cut", dto.CutOracleId);
                }

                results.Add(new UpgradeSuggestion
                {
                    AddCard      = addCard,
                    AddRationale = dto.AddRationale ?? string.Empty,
                    TargetRole   = gap.Role,
                    CutCard      = cutCard,
                    CutRationale = cutRationale,
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeckUpgrade: selection failed for {Role}", gap.Role);
            return [];
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void RecordUsage(string stage, string model, LlmResponse response)
    {
        UsageTracker?.RecordCall(
            stage, model,
            response.Usage.InputTokens,
            response.Usage.OutputTokens,
            response.Usage.CacheCreationInputTokens ?? 0,
            response.Usage.CacheReadInputTokens     ?? 0);
    }

    private static Task Report(Func<string, Task>? progress, string message) =>
        progress?.Invoke(message) ?? Task.CompletedTask;

    private static string RoleLabel(CardRole role) => role switch
    {
        CardRole.CardAdvantage      => "Card Advantage",
        CardRole.TargetedDisruption => "Targeted Disruption",
        CardRole.MassDisruption     => "Mass Disruption",
        _                           => role.ToString(),
    };

    // ── Private DTO ────────────────────────────────────────────────────────

    private sealed class UpgradeSuggestionDto
    {
        [JsonPropertyName("add_oracle_id")]
        public string? AddOracleId  { get; init; }

        [JsonPropertyName("add_rationale")]
        public string? AddRationale { get; init; }

        [JsonPropertyName("cut_oracle_id")]
        public string? CutOracleId  { get; init; }

        [JsonPropertyName("cut_rationale")]
        public string? CutRationale { get; init; }
    }
}
