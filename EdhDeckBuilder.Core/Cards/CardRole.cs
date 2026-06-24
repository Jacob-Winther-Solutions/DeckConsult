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
    Payoff,               // win conditions and theme payoffs
    Synergy,              // glue that supports the deck's theme
    Utility,
}

/// <summary>Where a role assignment came from, so the UI can surface provenance and confidence.</summary>
public enum ClassificationSource
{
    Manual,
    Edhrec,
    Heuristic,
    Llm,
}
