using Anthropic.Models.Messages;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Core.Rules;
using System.Text;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Prompts;

/// <summary>
/// Versioned prompts and tool definition for the card selection step.
/// Used by LlmSelector (Sonnet, temperature 0.6, forced tool call).
/// </summary>
public static class SelectionPrompt
{
    public const string ToolName = "select_cards";

    public const string SystemPrompt =
        """
        You are an expert Magic: the Gathering Commander (EDH) deck builder. Your task is to rank candidate cards for a specific role in a Commander deck, from best to worst.

        ## Ranking criteria (apply in this order)
        1. **Role fulfillment** — How directly and reliably the card performs the requested function in this specific deck
        2. **Synergy** — How well the card interacts with the commanders and other cards already chosen
        3. **Efficiency** — Mana cost relative to effect; cheaper is generally better at the same power level
        4. **EDHREC inclusion** — Higher means more community-tested for this commander; use as a tiebreaker, not a primary driver
        5. **Flexibility** — Cards that meaningfully cover a secondary role are preferred over single-purpose cards when cost is similar

        ## Output rules
        - Rank every candidate provided — do not omit any.
        - Rank 1 is the best, rank N (the total count) is the worst.
        - The rationale must be 1–2 sentences explaining why this card earns its position in THIS specific deck. Mention the commander by name, a concrete mechanical interaction, or a specific synergy with the archetype or theme. Every rationale must be deck-specific — a rationale that could be copy-pasted unchanged to a different deck is wrong.
        - FORBIDDEN rationale patterns (do not use these or any paraphrase of them): "top recommendation", "highly recommended", "popular choice", "community staple", "widely played", "selected from", "one of the best", "commonly seen", or any reference to EDHREC, Scryfall, Archidekt, or any other external tool or data source. If you cannot find a deck-specific reason, explain what the card does and why that effect is valuable at this power level in this commander's colours.
        - Return the oracle_id values exactly as provided — do not invent or modify any.
        """;

    /// <summary>Tool definition with cache control so Anthropic can cache the schema across calls.</summary>
    public static Tool Tool { get; } = new()
    {
        Name = ToolName,
        Description = "Rank the candidate cards from best (rank 1) to worst for the requested role in this Commander deck.",
        InputSchema = BuildSchema(),
        Strict = true,
        CacheControl = new CacheControlEphemeral(),
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

        sb.AppendLine($"Role needed: {role}");
        AppendBracketGuidance(sb, context.Constraints.Bracket);

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

        sb.AppendLine();
        sb.AppendLine($"Rank the following {candidates.Count} {role} candidates from best to worst using the {ToolName} tool:");

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

    private static InputSchema BuildSchema()
    {
        const string json = """
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

        using var doc = JsonDocument.Parse(json);
        return InputSchema.FromRawUnchecked(
            doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()));
    }
}
