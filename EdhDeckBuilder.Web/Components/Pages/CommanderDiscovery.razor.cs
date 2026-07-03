using EdhDeckBuilder.Agent.Authentication;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Pages;

public partial class CommanderDiscovery : ComponentBase
{
    [Inject] private SessionApiKeyProvider Keys { get; set; } = default!;

    private string _activeTab = "guided";

    private void SetTab(string tab)
    {
        _activeTab = tab;
        StateHasChanged();
    }
}
