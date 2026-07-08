using Anthropic.Models.Messages;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Core.Cards;
using System.Text;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Prompts;

/// <summary>
/// Versioned prompts and tool definition for the card classification step.
/// Used by LlmClassifier (Haiku, temperature 0.1, forced tool call).
/// </summary>
public static class ClassificationPrompt
{
    public const string ToolName = "classify_cards";
    private static InstrumentationOptions? _options;

    public const string SystemPrompt =
        """
        You are an expert Magic: the Gathering Commander (EDH) deck builder classifying cards for a specific deck.

        ## Roles
        Each card has exactly one primary role — the role it is most often played for in this deck context.
        Secondary roles are additional contributions the card makes; only add a secondary role if the card meaningfully fulfils it, not just incidentally.

        If a card does not fit any role in the deck context, assign it the **Unmatched** role.
        The fill engine will decide whether to include it based on remaining deck slots.

        **Land** — Lands used for mana. Assign this role only to actual land cards.

        **Ramp** — Cards that accelerate mana production beyond one land per turn: mana rocks, land ramp spells, mana dorks, rituals.

        **CardAdvantage** — Cards that generate net card equity: you must end up with more cards in hand or exile than you started with. A card qualifies if it draws 2+ cards, creates multiple impulse-draw effects, or replaces itself and does something else relevant. Cantrips (draw exactly 1), looting (draw X discard X), and rummaging (discard X draw X) do not qualify — they trade one card for one card and belong in Synergy or another more specific role.

        **TargetedDisruption** — Spot removal, counterspells, bounce, and exile targeting one permanent or spell.

        **MassDisruption** — Board wipes, global effects, stax pieces (Ghostly Prison, Propaganda). Affects many permanents at once.

        **Tutor** — Cards whose primary value is searching your library for a specific card.

        **Protection** — Hexproof, indestructible, phasing, shroud, regeneration, and other effects that keep your permanents alive.

        **Recursion** — Cards that return other cards from the graveyard: Regrowth, Eternal Witness, Animate Dead.

        **Plan** — The core strategy cards that directly execute what this deck is trying to do. Highly commander-dependent. In a tokens deck, token makers are Plan; in voltron, equipment and auras are Plan; in spellslinger, the spells being cast are Plan.

        **Payoff** — Cards that reward or multiply the plan without being the plan itself: Purphoros in a tokens deck, Anointed Procession, damage doublers. Converts the plan into a win.

        **Synergy** — Glue cards that support the deck broadly without fitting a more specific role.

        ## Role Relations (for secondary roles only)
        Only assign secondary roles when the secondary effect is **mechanically core to the card's function in the deck**, not minor or incidental.
        - **Always**: The card simultaneously provides both roles (e.g. Black Market Connections gives Ramp AND CardAdvantage at the same time).
        - **Modal**: The player chooses between roles when the card is played (e.g. Jeska's Will without commander gives mana OR draws — not both).
        - **Transform**: The card transitions from one role to another over the course of a game (e.g. Hedron Archive ramps first, then sacrifices to draw).

        **Limit secondary roles:** Most cards should have 0–1 secondary role. Only add a second secondary if both are genuinely major to the card's value.

        ## Land Credit (back face land quality)
        Assign a non-zero land_credit ONLY when the back face is a Land type (check the TypeLine for "// Land").
        Zero for all other cards — including DFCs whose back face is not a land.
        - 0.0: No land back face (use for all non-DFCs and DFCs with non-land back faces)
        - 0.1–0.3: Strong spell, weak land back (e.g. Agadeem's Awakening ≈ 0.3)
        - 0.4–0.6: Both sides viable — you would genuinely consider playing it as a land
        - 0.7–0.9: Land side is often the better choice
        - 1.0: You will almost always play it as a land

        ## Critical Rule
        Return the oracle_id values exactly as supplied — do not modify, invent, or omit any.
        """;

    /// <summary>Initialize the prompt with instrumentation options (enables/disables reasoning).</summary>
    public static void SetInstrumentationOptions(InstrumentationOptions options)
    {
        _options = options;
    }

    /// <summary>Whether classification responses should include a per-card <c>reasoning</c> field.</summary>
    public static bool IsReasoningEnabled => _options?.EnableClassificationReasoning == true;

    /// <summary>Tool definition with cache control so Anthropic can cache the schema across calls.</summary>
    public static Tool Tool
    {
        get => new()
        {
            Name = ToolName,
            Description = "Classify each candidate card into its primary role and any secondary roles for this Commander deck.",
            InputSchema = BuildSchema(),
            Strict = true,
            CacheControl = new CacheControlEphemeral(),
        };
    }

    public static string FormatUserMessage(IReadOnlyList<CardCandidate> candidates, IReadOnlyList<Card> commanders)
    {
        var sb = new StringBuilder();
        sb.Append("Commanders: ");
        sb.AppendJoin(", ", commanders.Select(c => c.Name));
        sb.AppendLine();
        sb.AppendLine();
        sb.Append($"Classify the following {candidates.Count} cards for this Commander deck using the {ToolName} tool.");

        foreach (var c in candidates)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"oracle_id: {c.Card.OracleId}");
            sb.AppendLine($"Name: {c.Card.Name}");
            if (!string.IsNullOrEmpty(c.Card.ManaCost))
                sb.AppendLine($"Mana Cost: {c.Card.ManaCost}");
            sb.AppendLine($"Type: {c.Card.TypeLine}");
            if (!string.IsNullOrWhiteSpace(c.Card.OracleText))
                sb.AppendLine($"Text: {c.Card.OracleText}");
            if (!string.IsNullOrWhiteSpace(c.Card.BackFaceTypeLine))
            {
                sb.AppendLine($"Back face type: {c.Card.BackFaceTypeLine}");
                if (!string.IsNullOrWhiteSpace(c.Card.BackFaceText))
                    sb.AppendLine($"Back face text: {c.Card.BackFaceText}");
            }
            sb.AppendLine($"EDHREC Inclusion: {c.Inclusion:P0}");
            if (!string.IsNullOrWhiteSpace(c.Section))
                sb.AppendLine($"Section: {c.Section}");
        }

        return sb.ToString();
    }

    private static InputSchema BuildSchema()
    {
        const string roleEnum = """["Land","Ramp","CardAdvantage","TargetedDisruption","MassDisruption","Tutor","Protection","Recursion","Plan","Payoff","Synergy","Unmatched"]""";

        // Build properties object dynamically based on whether reasoning is enabled
        var properties = new StringBuilder();
        properties.Append($$"""
              "oracle_id":    { "type": "string" },
              "primary_role": { "type": "string", "enum": {{roleEnum}} },
              "secondary": {
                "type": "array",
                "items": {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "role":     { "type": "string", "enum": {{roleEnum}} },
                    "relation": { "type": "string", "enum": ["Always","Modal","Transform"] },
                    "weight":   { "type": "number" }
                  },
                  "required": ["role","relation","weight"]
                }
              },
              "land_credit": { "type": "number" }
            """);

        if (_options?.EnableClassificationReasoning == true)
        {
            properties.Append(",\n              \"reasoning\": { \"type\": \"string\" }");
        }

        var json = $$"""
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "classifications": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      {{properties}}
                    },
                    "required": ["oracle_id","primary_role","secondary","land_credit"]
                  }
                }
              },
              "required": ["classifications"]
            }
            """;

        using var doc = JsonDocument.Parse(json);
        return InputSchema.FromRawUnchecked(
            doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()));
    }
}
