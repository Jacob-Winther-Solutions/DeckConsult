using EdhDeckBuilder.Infrastructure.Edhrec.Dto;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

/// <summary>
/// Abstraction for EDHREC API client, enabling testing and mocking.
/// </summary>
internal interface IEdhrecClient
{
    Task<EdhrecPage?> GetCommanderPageAsync(string slug, CancellationToken ct = default);
    Task<EdhrecPage?> GetAverageDeckPageAsync(string slug, CancellationToken ct = default);
    Task<EdhrecPartnerPage?> GetPartnersPageAsync(CancellationToken ct = default);
    Task<EdhrecPage?> GetPartnerPairRecommendationsAsync(string firstSlug, string secondSlug, CancellationToken ct = default);

    /// <summary>
    /// Returns the commander-specific theme-filtered page from
    /// <c>/pages/commanders/{commanderSlug}/{themeSlug}.json</c>.
    /// Returns <see langword="null"/> on 404 (no theme page for this commander).
    /// </summary>
    Task<EdhrecPage?> GetCommanderThemePageAsync(string commanderSlug, string themeSlug, CancellationToken ct = default);

    /// <summary>
    /// Returns the global theme tag page from <c>/pages/tags/{themeSlug}.json</c>.
    /// Cardlists whose header contains "Commander" are commander entries; all others are cards.
    /// Returns <see langword="null"/> on 404.
    /// </summary>
    Task<EdhrecPage?> GetTagsPageAsync(string themeSlug, CancellationToken ct = default);
}
