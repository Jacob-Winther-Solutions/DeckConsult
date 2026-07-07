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
}
