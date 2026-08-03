using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent;

/// <summary>
/// Resolves card names against <see cref="ICardRepository"/> and checks color identity compatibility.
/// </summary>
public sealed class LockedCardValidator(ICardRepository cardRepository) : ILockedCardValidator
{
    public async Task<LockedCardValidationResult> ValidateAsync(
        IReadOnlyList<string> cardNames,
        Color commanderColorIdentity,
        CancellationToken ct = default)
    {
        // Deduplicate while preserving order, ignore blank lines.
        var names = cardNames
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Resolve all names in parallel.
        var lookups = await Task.WhenAll(names.Select(n => cardRepository.GetByNameAsync(n, ct)));

        var valid       = new List<Card>();
        var unrecognized = new List<string>();
        var wrongColor  = new List<Card>();

        for (int i = 0; i < names.Count; i++)
        {
            var card = lookups[i];
            if (card is null)
            {
                unrecognized.Add(names[i]);
                continue;
            }

            if (!card.ColorIdentity.IsWithin(commanderColorIdentity))
                wrongColor.Add(card);
            else
                valid.Add(card);
        }

        return new LockedCardValidationResult
        {
            ValidCards       = valid,
            UnrecognizedNames = unrecognized,
            WrongColorCards  = wrongColor,
        };
    }
}
