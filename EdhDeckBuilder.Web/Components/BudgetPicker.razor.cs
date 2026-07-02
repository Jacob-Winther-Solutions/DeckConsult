using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components;

public partial class BudgetPicker
{
    [Parameter] public EventCallback<BudgetSelection> OnChanged { get; set; }
    [Parameter] public BudgetSelection? InitialBudget { get; set; }

    private string _maxCardPriceText        = "";
    private string _totalBudgetText         = "";
    private bool   _maxPriceUnrestricted    = true;
    private bool   _totalBudgetUnrestricted = true;

    protected override void OnInitialized()
    {
        if (InitialBudget is not null)
        {
            if (InitialBudget.MaxCardPriceUsd.HasValue)
            {
                _maxCardPriceText = InitialBudget.MaxCardPriceUsd.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                _maxPriceUnrestricted = false;
            }

            if (InitialBudget.TotalBudgetUsd.HasValue)
            {
                _totalBudgetText = InitialBudget.TotalBudgetUsd.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                _totalBudgetUnrestricted = false;
            }
        }
    }

    public BudgetSelection CurrentValue =>
        new(ParsedMaxCardPrice, ParsedTotalBudget);

    private decimal? ParsedMaxCardPrice =>
        _maxPriceUnrestricted ? null :
        decimal.TryParse(_maxCardPriceText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

    private decimal? ParsedTotalBudget =>
        _totalBudgetUnrestricted ? null :
        decimal.TryParse(_totalBudgetText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

    private async Task OnMaxUnrestrictedChanged(bool value)
    {
        _maxPriceUnrestricted = value;
        await NotifyChanged();
    }

    private async Task OnTotalUnrestrictedChanged(bool value)
    {
        _totalBudgetUnrestricted = value;
        await NotifyChanged();
    }

    private async Task OnMaxCardPriceChanged(ChangeEventArgs e)
    {
        _maxCardPriceText = e.Value?.ToString() ?? "";
        await NotifyChanged();
    }

    private async Task OnTotalBudgetChanged(ChangeEventArgs e)
    {
        _totalBudgetText = e.Value?.ToString() ?? "";
        await NotifyChanged();
    }

    private async Task NotifyChanged() =>
        await OnChanged.InvokeAsync(CurrentValue);
}
