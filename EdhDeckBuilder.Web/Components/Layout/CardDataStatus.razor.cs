using EdhDeckBuilder.Infrastructure.Scryfall;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;

namespace EdhDeckBuilder.Web.Components.Layout;

public partial class CardDataStatus : ComponentBase
{
    [Inject] private ICardRefreshService CardRefreshService { get; set; } = default!;
    [Inject] private IHostEnvironment Env { get; set; } = default!;

    private DateTimeOffset? _lastRefreshed;
    private bool _isRefreshing;
    private bool IsDevelopment => Env.IsDevelopment();

    protected override void OnInitialized()
        => _lastRefreshed = CardRefreshService.GetLastRefreshed();

    private async Task TriggerRefreshAsync()
    {
        _isRefreshing = true;
        StateHasChanged();
        try
        {
            await CardRefreshService.RefreshAsync();
            _lastRefreshed = CardRefreshService.GetLastRefreshed();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private string FormatLastRefreshed()
    {
        if (_lastRefreshed is null) return "never";
        var age = DateTimeOffset.UtcNow - _lastRefreshed.Value;
        if (age.TotalMinutes < 1) return "just now";
        if (age.TotalHours < 1)   return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalDays < 1)    return $"{(int)age.TotalHours}h ago";
        if (age.TotalDays < 2)    return "yesterday";
        return $"{(int)age.TotalDays}d ago";
    }
}
