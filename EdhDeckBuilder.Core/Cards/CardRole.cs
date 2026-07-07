namespace EdhDeckBuilder.Core.Cards;

/// <summary>
/// The functional "job" a card does in a deck — the buckets the UI groups by and that deck
/// templates set targets for. Named after function rather than mechanism: a Counterspell and
/// a Path to Exile are both Targeted Disruption; a Wrath of God and a Ghostly Prison are both
/// Mass Disruption. Multi-role overlap is handled by <see cref="RoleProfile"/>.
/// </summary>
public enum CardRole
{
    Unclassified = 0,
    Land,
    Ramp,
    CardAdvantage,        // draw, selection, filtering, impulse draw
    TargetedDisruption,   // spot removal, counterspells, bounce
    MassDisruption,       // board wipes, stax, taxing effects (Ghostly Prison, Propaganda)
    Tutor,
    Protection,           // hexproof, indestructible, regenerate, phasing — defensive only
    Recursion,

    /// <summary>
    /// The core strategy — cards that directly embody what the deck is trying to do. In a
    /// Tokens deck, "Raise the Alarm" is the Plan: it makes tokens, which is the entire point.
    /// In a Spellslinger deck, cantrips and instants are the Plan. In Voltron, equipment and
    /// auras are the Plan. This is the role to maximise for overlap, since everything else in
    /// the deck exists to enable or protect it.
    /// </summary>
    Plan,

    Payoff,               // win conditions that convert the plan into a win (Purphoros, Anointed Procession)
    Synergy,              // glue that supports the plan but isn't the plan itself
    Unmatched,            // card did not match any primary role in the classifier
}

/// <summary>Where a role assignment came from, so the UI can surface provenance and confidence.</summary>
public enum ClassificationSource
{
    Manual,
    Edhrec,
    Heuristic,
    Llm,
}
