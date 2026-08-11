using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Pages.Discovery;

public partial class CommanderDiscoveryInfoModal : ComponentBase
{
    [Parameter] public bool          Visible { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
}
