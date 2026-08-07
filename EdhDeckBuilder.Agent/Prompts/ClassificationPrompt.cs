using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Core.Cards;
using System.Text;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Prompts;

/// <summary>
/// Versioned prompts and tool definition for the card classification step.
/// Used by ClaudeClassifier (Haiku, temperature 0.1, forced tool call).
/// </summary>
public static class ClassificationPrompt
{
    public const string ToolName = "classify_cards";
    private static InstrumentationOptions? _options;

    public const string SystemPrompt =
        """
        You are an expert Magic: the Gathering deck builder classifying cards for a specific deck.

        ## Roles
        Each card has exactly one primary role — the role it is most often played for in this deck context.
        Secondary roles are additional contributions the card makes; only add a secondary role if the card meaningfully fulfils it, not just incidentally.

        If a card does not fit any role in the deck context, assign it the **Unmatched** role.
        The fill engine will decide whether to include it based on remaining deck slots.

        **Land** — Lands used for mana. Assign this role only to actual land cards.

        **Ramp** — Cards that accelerate mana production beyond one land per turn: mana rocks, land ramp spells, mana dorks, rituals, and effects that grant additional land drops per turn (e.g. Exploration, Oracle of Mul Daya, effects like "you may play an additional land this turn").

        **CardAdvantage** — Cards that generate net card equity: you must end up with more cards in hand or exile than you started with. A card qualifies if it draws 2+ cards, creates multiple impulse-draw effects, or replaces itself and does something else relevant. Cantrips (draw exactly 1), looting (draw X discard X), and rummaging (discard X draw X) do not qualify — they trade one card for one card and belong in Synergy or another more specific role.

        **TargetedDisruption** — Spot removal, counterspells, bounce, and exile targeting one permanent or spell, and combat-denial effects targeting a single creature (tap effects, can't attack this turn, can't block this turn, Maze of Ith-style effects). Global combat-denial that affects all attackers or all blockers (Fog, Propaganda) belongs in MassDisruption instead.

        **MassDisruption** — Board wipes, global effects, stax pieces (Ghostly Prison, Propaganda). Affects many permanents at once.

        **Tutor** — Cards whose primary value is searching your library for a specific card, either with or without certain criteria set, which for instance could include card type or color restrictions.

        **Protection** — Hexproof, indestructible, phasing, shroud, regeneration, protection from colors/types, and other effects that keep your permanents alive against removal or combat damage.

        **Recursion** — Cards that return other cards from the graveyard to either your hand or the battlefield: Regrowth, Eternal Witness, Animate Dead.

        **Plan** — The core strategy cards that directly execute what this deck is trying to do. Highly commander-dependent. In a tokens deck, token makers are Plan; in voltron, equipment and auras are Plan; in spellslinger, the spells being cast are Plan.

        **Payoff** — Cards that reward or multiply the plan without being the plan itself: Purphoros in a tokens deck, Anointed Procession, damage doublers. Converts the plan into a win.

        **Synergy** — Glue cards that support the deck broadly without fitting a more specific role. Cards that fit the theme without driving it.

        ## Role Relations (for secondary roles only)
        Only assign secondary roles when the secondary effect is **mechanically core to the card's function in the deck**, not minor or incidental.
        - **Always**: The card simultaneously provides both roles (e.g. Black Market Connections gives Ramp AND CardAdvantage at the same time).
        - **Modal**: The player chooses between roles when the card is played (e.g. Jeska's Will without commander gives mana OR draws — not both).
        - **Transform**: The card transitions from one role to another over the course of a game (e.g. Hedron Archive ramps first, then sacrifices to draw).

        **Limit secondary roles:** Most cards should have 0–1 secondary role. Only add a second secondary if both are genuinely major to the card's value.

        ## Role priority (when a card genuinely fits multiple roles)
        Use this tiebreaker order to pick the primary role — assign the first role that clearly applies:
        **Tutor > CardAdvantage > Ramp > TargetedDisruption > MassDisruption > Protection > Recursion > Plan > Payoff > Synergy > Land > Unmatched**

        Override only when the deck context makes a lower-priority role clearly dominant. Example: a land-searching spell in a deck that needs one specific land for a combo is Tutor, not Ramp.

        ## Disambiguating similar roles

        **Ramp vs. Synergy — cost reducers:**
        Permanent cost reducers for spells you cast regularly count as **Ramp**, not Synergy. Examples: Urza's Incubator, Herald's Horn, Semblance Anvil, Goblin Electromancer, Birgi, God of Storytelling. The test: does the card produce an ongoing mana advantage that lets you cast more or larger spells per turn? If yes, it is Ramp. Classify as Synergy only if the cost reduction is too narrow to matter in this specific deck.

        **Plan vs. Payoff — action vs. reward:**
        Plan cards *do* the deck's action. Payoff cards *score points for* doing the action.
        In a combat-focused deck: equipment that makes your creature bigger is Plan; a card that deals damage to each opponent whenever your creature deals combat damage is Payoff.
        Quick test — if you removed this card, would the deck still be able to execute its strategy? If yes, it is Payoff or Synergy. If no, it is Plan.

        **Plan vs. Synergy — core vs. support:**
        Plan cards are primary enablers of the strategy; taking them out makes the deck noticeably less functional. Synergy cards support the strategy without being central to it. In a spellslinger deck, every cheap instant that triggers storm is Plan; a cost reducer is Ramp; a creature that merely benefits from spells being cast is Synergy.

        **Unmatched — off-strategy only:**
        Use Unmatched for cards that are genuinely irrelevant to this deck's strategy — wrong colors, contradict the gameplan, or provide value the deck cannot use. Do NOT use Unmatched for weak or suboptimal cards. A mediocre Ramp card is still Ramp. A worse-than-average removal spell is still TargetedDisruption.

        **TargetedDisruption vs. MassDisruption — versatile spells:**
        Assign the role that matches how the card is *primarily* played in this deck context. Cyclonic Rift without overload is TargetedDisruption; with overload it is MassDisruption. For a deck that will almost always overload it, classify as MassDisruption and note the modal relation in secondary.

        ## Role examples

        | Role | Canonical examples |
        |---|---|
        | Land | Command Tower, Arcane Sanctum, Evolving Wilds, Ancient Tomb, Temple of Deceit |
        | Ramp | Sol Ring, Arcane Signet, Cultivate, Kodama's Reach, Faeburrow Elder, Urza's Incubator |
        | CardAdvantage | Rhystic Study, Phyrexian Arena, Harmonize, Painful Truths, Consecrated Sphinx |
        | TargetedDisruption | Swords to Plowshares, Path to Exile, Counterspell, Beast Within, Chaos Warp |
        | MassDisruption | Wrath of God, Cyclonic Rift (overloaded), Vandalblast, Ghostly Prison, Propaganda |
        | Tutor | Demonic Tutor, Vampiric Tutor, Worldly Tutor, Enlightened Tutor, Mystical Tutor |
        | Protection | Lightning Greaves, Swiftfoot Boots, Teferi's Protection, Heroic Intervention |
        | Recursion | Eternal Witness, Regrowth, Animate Dead, Reanimate, Unearth |
        | Plan | Token creators in token decks, equipment in voltron, mana-expensive creatures in reanimator |
        | Payoff | Purphoros God of the Forge, Impact Tremors, Anointed Procession, Doubling Season |
        | Synergy | Permanents with cast-triggers in spellslinger decks, discard outlets in reanimator |

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

    /// <summary>
    /// Tool definition re-evaluated each access so the schema reflects the current reasoning flag.
    /// </summary>
    public static LlmToolDefinition ToolDefinition => new()
    {
        Name        = ToolName,
        Description = "Classify each candidate card into its primary role and any secondary roles for this Commander deck.",
        InputSchema = JsonNode.Parse(BuildSchemaJson())!,
    };

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

    // ── Plan description ──────────────────────────────────────────────────────

    public const string PlanDescriptionToolName = "describe_plan";

    public const string PlanDescriptionSystemPrompt =
        "You are an expert Magic: the Gathering deck analyst. Describe deck strategies concisely and accurately.";

    public static LlmToolDefinition PlanDescriptionToolDefinition => new()
    {
        Name        = PlanDescriptionToolName,
        Description = "Describe the deck's core strategy based on its Plan cards.",
        InputSchema = JsonNode.Parse("""
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "description": { "type": "string" }
              },
              "required": ["description"]
            }
            """)!,
    };

    public static string FormatPlanDescriptionMessage(IReadOnlyList<Card> commanders, IReadOnlyList<Card> planCards)
    {
        var sb = new StringBuilder();
        sb.Append("Commanders: ");
        sb.AppendJoin(", ", commanders.Select(c => c.Name));
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine($"The following {planCards.Count} card(s) have been classified as this deck's Plan (core strategy):");

        foreach (var card in planCards)
        {
            sb.AppendLine("---");
            sb.AppendLine($"Name: {card.Name}");
            if (!string.IsNullOrEmpty(card.ManaCost))
                sb.AppendLine($"Mana Cost: {card.ManaCost}");
            sb.AppendLine($"Type: {card.TypeLine}");
            if (!string.IsNullOrWhiteSpace(card.OracleText))
                sb.AppendLine($"Text: {card.OracleText}");
        }

        sb.AppendLine();
        sb.AppendLine(
            "In 2–3 sentences, describe what this deck is trying to do. " +
            "Be specific about the win condition or core mechanism, mentioning key card interactions where relevant. " +
            "Do not mention card counts or roles — focus on the strategy.");

        return sb.ToString();
    }

    private static string BuildSchemaJson()
    {
        const string roleEnum = """["Land","Ramp","CardAdvantage","TargetedDisruption","MassDisruption","Tutor","Protection","Recursion","Plan","Payoff","Synergy","Unmatched"]""";

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
            properties.Append(",\n              \"reasoning\": { \"type\": \"string\" }");

        return $$"""
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
    }

}
