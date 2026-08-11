using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Pages.Analysis;

public partial class DeckAnalyzerInfoModal : ComponentBase
{
    [Parameter] public bool          Visible { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
}
