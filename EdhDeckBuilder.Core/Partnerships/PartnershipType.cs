namespace EdhDeckBuilder.Core.Partnerships;

/// <summary>
/// Classification of partnership mechanics between legendary creatures.
/// Supports existing and anticipated partnership variants from Scryfall.
/// </summary>
public enum PartnershipType
{
    /// <summary>Generic "Partner" keyword — any two legendary creatures with this keyword can partner.</summary>
    Partner,

    /// <summary>Specific "Partner with [CardName]" — this card partners only with the named card.</summary>
    PartnerWith,

    /// <summary>Background creature — pairs with legendary creatures that explicitly support Backgrounds.</summary>
    Background,

    /// <summary>"Friends Forever" keyword — a social bond between legendary creatures.</summary>
    FriendsForever,

    /// <summary>"Doctor's Companion" keyword — a specialty bond for Doctors and Companions.</summary>
    DoctorsCompanion,

    /// <summary>"Partner - Survivors" keyword — partnership restricted to Survivor creature types.</summary>
    PartnerSurvivors,

    /// <summary>Custom or unknown partnership type — reserved for future variants not yet classified.</summary>
    Custom,
}
