using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using System.Text;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Prompts;

public static class UpgradeSelectionPrompt
{
    // ── Gap prioritization ──────────────────────────────────────────────────

    public const string PrioritizationToolName = "prioritize_gaps";

    public const string PrioritizationSystemPrompt =
        """
        You are a Magic: the Gathering deck advisor. A player has described a problem with their Commander deck.
        Given a list of role coverage gaps, order them from most to least relevant to the player's stated issue.
        Return ALL gap roles in order — do not omit any.
        """;

    public static LlmToolDefinition PrioritizationToolDefinition { get; } = new()
    {
        Name        = PrioritizationToolName,
        Description = "Return the gap roles ordered from most to least relevant to the player's stated problem.",
        InputSchema = JsonNode.Parse("""
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "prioritized_roles": {
                  "type": "array",
                  "items": { "type": "string" }
                }
              },
              "required": ["prioritized_roles"]
            }
            """)!,
    };

    public static string FormatPrioritizationMessage(
        IReadOnlyList<RoleGap> gaps,
        string userFeedback,
        IReadOnlyList<Card> commanders)
    {
        var sb = new StringBuilder();
        sb.Append("Commander(s): ");
        sb.AppendJoin(", ", commanders.Select(c => c.Name));
        sb.AppendLine();
        sb.AppendLine($"Player reports: \"{userFeedback}\"");
        sb.AppendLine();
        sb.AppendLine("Coverage gaps (ordered by shortfall):");
        foreach (var gap in gaps)
            sb.AppendLine($"  {RoleName(gap.Role)} — actual {gap.ActualCoverage:0.#}, ideal {gap.IdealTarget}, shortfall {gap.Shortfall:0.#}");
        sb.AppendLine();
        sb.AppendLine("Reorder the roles so the most relevant to the player's issue comes first.");
        return sb.ToString();
    }

    // ── Upgrade selection ───────────────────────────────────────────────────

    public const string SelectionToolName = "suggest_upgrades";

    public const string SelectionSystemPrompt =
        """
        You are an expert Magic: the Gathering deck advisor recommending targeted upgrades for an existing Commander deck.

        For each suggestion, identify:
        1. The best card to ADD from the candidate pool to fill the stated gap role.
        2. The best card to CUT from the current deck to make room.

        ## Criteria for additions:
        - Best fulfills the target gap role in this specific deck and commander context.
        - Strong synergy with the commanders and existing strategy.
        - Mana-efficient relative to effect; cheaper is generally better at the same power level.
        - Must be within the budget constraint if one is stated.

        ## Criteria for cuts:
        - Strongly prefer cards from roles where actual coverage exceeds the baseline ideal (over-filled roles).
        - Otherwise, cut the least impactful card in any role — weakest effect, least synergy, or highest mana cost relative to what it does.
        - Do NOT suggest cutting a commander.
        - Do NOT suggest the same cut card more than once across the three suggestions.

        ## Output rules:
        - Provide exactly 3 suggestions, ranked best to worst.
        - add_oracle_id must exactly match an oracle_id from the candidate pool.
        - cut_oracle_id must exactly match an oracle_id from the current deck cards list.
        - Rationale: 1–2 sentences, deck-specific — name the commander or a concrete mechanic. No generic phrases like "widely played" or "top pick".
        """;

    public static LlmToolDefinition SelectionToolDefinition { get; } = new()
    {
        Name        = SelectionToolName,
        Description = "Suggest 3 ranked upgrade pairs (add + cut) to address a coverage gap.",
        InputSchema = JsonNode.Parse("""
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "suggestions": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "add_oracle_id":  { "type": "string" },
                      "add_rationale":  { "type": "string" },
                      "cut_oracle_id":  { "type": "string" },
                      "cut_rationale":  { "type": "string" }
                    },
                    "required": ["add_oracle_id","add_rationale","cut_oracle_id","cut_rationale"]
                  }
                }
              },
              "required": ["suggestions"]
            }
            """)!,
    };

    public static string FormatSelectionMessage(
        RoleGap gap,
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<AnalyzedCard> deckCards,
        IReadOnlyDictionary<CardRole, double> actualCoverage,
        IReadOnlyList<Card> commanders,
        string? userFeedback,
        decimal? maxCardPriceUsd)
    {
        var sb = new StringBuilder();

        sb.Append("Commander(s): ");
        sb.AppendJoin(", ", commanders.Select(c => c.Name));
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(userFeedback))
            sb.AppendLine($"Player reports: \"{userFeedback}\"");

        if (maxCardPriceUsd.HasValue)
            sb.AppendLine($"Budget for additions: max ${maxCardPriceUsd:F2} per card");

        sb.AppendLine($"Gap to fill: {RoleName(gap.Role)} — actual {gap.ActualCoverage:0.#}, ideal {gap.IdealTarget}, shortfall {gap.Shortfall:0.#}");
        sb.AppendLine();

        // Coverage table — lets model see over-filled roles for cut candidates
        sb.AppendLine("Current coverage vs. Balanced baseline:");
        foreach (var (role, target) in DeckTemplate.Balanced.Targets)
        {
            var actual  = actualCoverage.GetValueOrDefault(role, 0.0);
            var status  = actual >= target.Max ? "OVER" : actual >= target.Min ? "OK" : "LOW";
            var marker  = role == gap.Role ? " ← target" : "";
            sb.AppendLine($"  {RoleName(role)}: {actual:0.#} / ideal {target.Ideal} [{status}]{marker}");
        }
        sb.AppendLine();

        // Current deck cards — the cut pool
        sb.AppendLine("Current deck cards (eligible cuts — oracle_id required for output):");
        foreach (var analyzed in deckCards.OrderBy(c => c.Roles.Primary.ToString()).ThenBy(c => c.Card.Name))
        {
            sb.AppendLine($"oracle_id: {analyzed.Card.OracleId}");
            sb.AppendLine($"Name: {analyzed.Card.Name}");
            sb.AppendLine($"Role: {RoleName(analyzed.Roles.Primary)}");
            sb.AppendLine($"Mana Value: {analyzed.Card.ManaValue:0.#}");
            sb.AppendLine(analyzed.Card.PriceUsd.HasValue
                ? $"Price: ${analyzed.Card.PriceUsd:F2}"
                : "Price: no data");
            sb.AppendLine("---");
        }
        sb.AppendLine();

        // Candidate pool for the add
        sb.AppendLine($"Suggest 3 upgrades from the following {candidates.Count} candidates for {RoleName(gap.Role)}:");
        foreach (var c in candidates)
        {
            sb.AppendLine();
            sb.AppendLine($"oracle_id: {c.Card.OracleId}");
            sb.AppendLine($"Name: {c.Card.Name}");
            if (!string.IsNullOrEmpty(c.Card.ManaCost))
                sb.AppendLine($"Mana Cost: {c.Card.ManaCost}");
            sb.AppendLine($"Type: {c.Card.TypeLine}");
            if (!string.IsNullOrWhiteSpace(c.Card.OracleText))
                sb.AppendLine($"Text: {c.Card.OracleText}");
            sb.AppendLine($"EDHREC Inclusion: {c.Inclusion:P0}");
            sb.AppendLine(c.Card.PriceUsd.HasValue
                ? $"Price: ${c.Card.PriceUsd:F2}"
                : "Price: no data");
        }

        return sb.ToString();
    }

    private static string RoleName(CardRole role) => role switch
    {
        CardRole.CardAdvantage      => "Card Advantage",
        CardRole.TargetedDisruption => "Targeted Disruption",
        CardRole.MassDisruption     => "Mass Disruption",
        _                           => role.ToString(),
    };
}
