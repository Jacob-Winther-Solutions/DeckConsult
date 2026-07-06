using EdhDeckBuilder.Core.Abstractions;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

/// <summary>
/// Provides authoritative partner pairing information from EDHREC.
/// Uses EDHREC's "Partner with" cardlist as the source of truth.
/// </summary>
internal sealed class PartnerPairingRepository : IPartnerPairingRepository
{
    private readonly SuggestionSource _suggestionSource;

    internal PartnerPairingRepository(SuggestionSource suggestionSource)
    {
        _suggestionSource = suggestionSource;
    }

    public async Task<IReadOnlyList<(string FirstCardName, string SecondCardName)>> GetPartnerWithPairsAsync(CancellationToken ct = default)
    {
        return await _suggestionSource.GetPartnerWithPairsAsync(ct);
    }
}
