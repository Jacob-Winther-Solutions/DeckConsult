using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Prompts;

/// <summary>
/// Single source of truth for card role definitions.
/// Used both to build the classification prompt and to show tooltips in the UI.
/// </summary>
public sealed record RoleDefinition(string DisplayName, string Description);

public static class CardRoleGlossary
{
    public static readonly IReadOnlyDictionary<CardRole, RoleDefinition> Definitions =
        new Dictionary<CardRole, RoleDefinition>
        {
            [CardRole.Land] = new("Land",
                "Lands used for mana. Only actual land cards get this role."),

            [CardRole.Ramp] = new("Ramp",
                "Cards that accelerate mana production beyond one land per turn: mana rocks, land ramp spells, mana dorks, rituals, and effects that grant additional land drops per turn " +
                "(e.g. Exploration, Oracle of Mul Daya, effects like \"you may play an additional land this turn\")."),

            [CardRole.CardAdvantage] = new("Card Advantage",
                "Cards that generate net card equity — you must end up with more cards in hand or exile than you started with. " +
                "Qualifies if it draws 2+ cards, creates multiple impulse-draw effects, or replaces itself and does something else relevant. " +
                "Cantrips (draw exactly 1), looting (draw X discard X), and rummaging (discard X draw X) do not qualify."),

            [CardRole.TargetedDisruption] = new("Targeted Disruption",
                "Spot removal, counterspells, bounce, and exile targeting one permanent or spell, and combat-denial effects targeting a single creature " +
                "(tap effects, can't attack this turn, can't block this turn, Maze of Ith-style effects). " +
                "Global combat-denial that affects all attackers or all blockers belongs in Mass Disruption instead."),

            [CardRole.MassDisruption] = new("Mass Disruption",
                "Board wipes, global effects, and stax pieces (e.g. Ghostly Prison, Propaganda). Affects many permanents at once."),

            [CardRole.Tutor] = new("Tutor",
                "Cards whose primary value is searching your library for a specific card, with or without restrictions on card type or color."),

            [CardRole.Protection] = new("Protection",
                "Hexproof, indestructible, phasing, shroud, regeneration, protection from colors/types, and other effects that keep your permanents alive against removal or combat damage."),

            [CardRole.Recursion] = new("Recursion",
                "Cards that return other cards from the graveyard to your hand or the battlefield (e.g. Regrowth, Eternal Witness, Animate Dead)."),

            [CardRole.Plan] = new("Plan",
                "The core strategy cards that directly execute what the deck is trying to do. Highly commander-dependent — " +
                "token makers in a tokens deck, equipment and auras in voltron, the spells being cast in spellslinger."),

            [CardRole.Payoff] = new("Payoff",
                "Cards that reward or multiply the plan without being the plan itself — damage doublers, token doublers, triggered win conditions. " +
                "Converts the plan into a win (e.g. Purphoros, Anointed Procession)."),

            [CardRole.Synergy] = new("Synergy",
                "Glue cards that support the deck's strategy broadly without fitting a more specific role. Fit the theme without driving it."),

            [CardRole.Unmatched] = new("Unmatched",
                "Cards that are genuinely off-strategy for this deck — wrong colors, contradict the gameplan, or provide value the deck cannot use. " +
                "Weak or suboptimal cards that still fit a role are NOT unmatched."),
        };

    /// <summary>Builds the ## Roles section for the classification prompt from the shared definitions.</summary>
    internal static string BuildRolesSection()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Roles");
        sb.AppendLine("Each card has exactly one primary role — the role it is most often played for in this deck context.");
        sb.AppendLine("Secondary roles are additional contributions the card makes; only add a secondary role if the card meaningfully fulfils it, not just incidentally.");
        sb.AppendLine();
        sb.AppendLine("If a card does not fit any role in the deck context, assign it the **Unmatched** role.");
        sb.AppendLine("The fill engine will decide whether to include it based on remaining deck slots.");
        sb.AppendLine();

        foreach (var (role, def) in Definitions)
        {
            sb.AppendLine($"**{def.DisplayName}** — {def.Description}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
