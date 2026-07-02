using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Pages;

public partial class CommanderBuilder : IDisposable
{
    [Inject] private SessionApiKeyProvider Keys       { get; set; } = default!;
    [Inject] private IApiKeyStateService   ApiKeyState { get; set; } = default!;

    private string _activeTab = "guided";

    protected override void OnInitialized() =>
        ApiKeyState.OnChange += OnApiKeyStateChanged;

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => ApiKeyState.OnChange -= OnApiKeyStateChanged;
}
