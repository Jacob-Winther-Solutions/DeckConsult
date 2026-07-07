using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Infrastructure.Edhrec.Dto;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

internal static class EdhrecMapper
{
    public static async Task<IReadOnlyList<CardCandidate>> ToCardCandidatesAsync(
        IEnumerable<EdhrecCardlist> cardlists,
        ICardRepository repository,
        ILogger logger,
        CancellationToken ct)
    {
        // Flatten all sections, deduplicating by name. When a card appears in multiple sections,
        // keep the entry with the highest inclusion and record that section — it is the strongest
        // signal for why the source considers this card relevant.
        var best = new Dictionary<string, (EdhrecCardView View, string Section)>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in cardlists)
        foreach (var view in list.Cardviews)
        {
            if (!best.TryGetValue(view.Name, out var existing) || Inclusion(view) > Inclusion(existing.View))
                best[view.Name] = (view, list.Header);
        }

        var result = new List<CardCandidate>(best.Count);
        foreach (var (view, section) in best.Values.OrderByDescending(e => Inclusion(e.View)))
        {
            // Try to lookup by Scryfall ID first (most reliable), then fall back to name
            Card? card = null;
            if (!string.IsNullOrEmpty(view.Id) && Guid.TryParse(view.Id, out var scryfallId))
            {
                card = await repository.GetByScryfallIdAsync(scryfallId, ct);
            }

            if (card is null)
            {
                card = await repository.GetByNameAsync(view.Name, ct);
            }

            if (card is null)
            {
                logger.LogDebug("EDHREC card {Name} (ID: {Id}) not found in local repository; skipping", view.Name, view.Id);
                continue;
            }
            result.Add(new CardCandidate(card, Inclusion(view), section));
        }
        return result;
    }

    private static double Inclusion(EdhrecCardView view)
        => view.PotentialDecks > 0 ? (double)view.NumDecks / view.PotentialDecks : 0;
}
