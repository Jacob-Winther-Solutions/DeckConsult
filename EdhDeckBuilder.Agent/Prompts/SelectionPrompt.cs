using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Core.Rules;
using System.Text;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Prompts;

/// <summary>
/// Versioned prompts and tool definition for the card selection step.
/// Used by ClaudeSelector (Sonnet, temperature 0.6, forced tool call).
/// </summary>
public static class SelectionPrompt
{
    public const string ToolName = "select_cards";

    public const string SystemPrompt =
        """
        You are an expert Magic: the Gathering deck builder. Your task is to rank candidate cards for a specific role in a Commander deck, from best to worst.

        ## Ranking criteria (apply in this order)
        1. **Role fulfillment** — How directly and reliably the card performs the requested function in this specific deck
        2. **Synergy** — How well the card interacts with the commanders and other cards already chosen
        3. **Efficiency** — Mana cost relative to effect; cheaper is generally better at the same power level
        4. **EDHREC inclusion** — Higher means more community-tested for this commander; use as a tiebreaker, not a primary driver
        5. **Flexibility** — Cards that meaningfully cover a secondary role are preferred over single-purpose cards when cost is similar

        ## Output rules
        - Rank every candidate provided — do not omit any.
        - Rank 1 is the best, rank N (the total count) is the worst.
        - The rationale must be a single sentence (≤15 words) explaining why this card is good for THIS specific deck. Be specific: name the commander or mention a concrete mechanic, not generic value.
        - FORBIDDEN rationale patterns (do not use these or any paraphrase of them): "top recommendation", "highly recommended", "popular choice", "community staple", "widely played", "selected from", "one of the best", "commonly seen", or any reference to EDHREC, Scryfall, Archidekt, or any other external tool or data source. If you cannot find a deck-specific reason, explain what the card does and why that effect is valuable at this power level in this commander's colours.
        - Return the oracle_id values exactly as provided — do not invent or modify any.
        """;

    public static LlmToolDefinition ToolDefinition { get; } = new()
    {
        Name        = ToolName,
        Description = "Rank the candidate cards from best (rank 1) to worst for the requested role in this Commander deck.",
        InputSchema = JsonNode.Parse(SchemaJson)!,
    };

    public static string FormatUserMessage(
        CardRole role,
        IReadOnlyList<FillCandidate> candidates,
        BuildContext context,
        BuildState state)
    {
        var sb = new StringBuilder();

        sb.Append("Commanders: ");
        sb.AppendJoin(", ", context.Commanders.Select(c => c.Name));
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(context.Constraints.DeckDescription))
            sb.AppendLine($"Deck intent: {context.Constraints.DeckDescription}");

        sb.AppendLine($"Role needed: {role}");
        AppendBracketGuidance(sb, context.Constraints.Bracket);
        AppendBudgetGuidance(sb, context.Constraints);

        if (!string.IsNullOrWhiteSpace(context.Constraints.CurveNote))
            sb.AppendLine($"Curve note: {context.Constraints.CurveNote}");

        if (context.Constraints.AdditionalHints.Count > 0)
        {
            sb.Append("Additional hints: ");
            sb.AppendJoin("; ", context.Constraints.AdditionalHints);
            sb.AppendLine();
        }

        // Briefly surface what's already been committed so the model can judge synergy
        var filledRoles = state.PrimaryCounts
            .Where(kv => kv.Value > 0)
            .Select(kv => $"{kv.Key} ×{kv.Value}");
        sb.Append("Slots already filled: ");
        sb.AppendJoin(", ", filledRoles);
        sb.AppendLine();

        // Compute request count based on role target + buffer
        var roleTarget = context.ResolvedTemplate.Targets.TryGetValue(role, out var target)
            ? target.Ideal
            : 10;
        var buffer = role switch
        {
            CardRole.Land => 5,           // land base has more variance
            CardRole.Ramp or CardRole.CardAdvantage => 4,
            _ => 2
        };
        var requestCount = Math.Min(roleTarget + buffer, candidates.Count);

        sb.AppendLine();
        sb.AppendLine($"Identify the top {requestCount} best candidates for {role} from the following {candidates.Count} options:");

        foreach (var fc in candidates)
        {
            var c = fc.Card;
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"oracle_id: {c.OracleId}");
            sb.AppendLine($"Name: {c.Name}");
            if (!string.IsNullOrEmpty(c.ManaCost))
                sb.AppendLine($"Mana Cost: {c.ManaCost}");
            sb.AppendLine($"Type: {c.TypeLine}");
            if (!string.IsNullOrWhiteSpace(c.OracleText))
                sb.AppendLine($"Text: {c.OracleText}");
            sb.AppendLine($"EDHREC Inclusion: {fc.Candidate.Inclusion:P0}");
            sb.AppendLine(c.PriceUsd.HasValue
                ? $"Price (USD): ${c.PriceUsd:F2}"
                : "Price (USD): no data");

            // Show secondary roles so the model can judge flexibility
            if (fc.Roles.Secondary.Count > 0)
            {
                var secondary = fc.Roles.Secondary.Select(s => $"{s.Role} ({s.Relation})");
                sb.Append("Also covers: ");
                sb.AppendJoin(", ", secondary);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void AppendBudgetGuidance(StringBuilder sb, SoftConstraints constraints)
    {
        if (constraints.MaxCardPriceUsd.HasValue)
            sb.AppendLine(
                $"Budget: max ${constraints.MaxCardPriceUsd:F2} per card. " +
                "Strongly deprioritize cards above this price and prefer affordable alternatives. " +
                "If no affordable card can fill the role well, pick the best available — it will be flagged as over budget.");

        if (constraints.TotalBudgetUsd.HasValue)
            sb.AppendLine(
                $"Total deck budget: ${constraints.TotalBudgetUsd:F2}. " +
                "Favor lower-cost cards to stay within the total spend.");
    }

    private static void AppendBracketGuidance(StringBuilder sb, Bracket bracket)
    {
        var profile = BracketLibrary.All[bracket];
        sb.AppendLine($"Bracket: {(int)bracket} — {profile.Name}: {profile.Description}");

        if (bracket <= Bracket.Two)
        {
            sb.Append("Game Changer cards are above this power level — rank them last if they appear. ");
            sb.Append("Game Changers: ");
            sb.AppendJoin(", ", GameChangersList.Cards.OrderBy(n => n));
            sb.AppendLine(".");
        }
        else if (bracket >= Bracket.Four)
        {
            sb.AppendLine(
                "Game Changer cards (Mana Crypt, Demonic Tutor, Rhystic Study, Cyclonic Rift, etc.) " +
                "are expected at this power level — rank them highly when relevant to the role.");
        }
    }

    private const string SchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "selections": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "oracle_id": { "type": "string" },
                  "rank":      { "type": "integer" },
                  "rationale": { "type": "string" }
                },
                "required": ["oracle_id","rank","rationale"]
              }
            }
          },
          "required": ["selections"]
        }
        """;

}
