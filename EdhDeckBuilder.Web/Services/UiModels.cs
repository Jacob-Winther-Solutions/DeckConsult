using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Web.Services;

public sealed record BracketSelection(Bracket Bracket, bool Enabled);
public sealed record BudgetSelection(decimal? MaxCardPriceUsd, decimal? TotalBudgetUsd);
