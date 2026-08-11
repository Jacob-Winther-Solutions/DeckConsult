using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Pages.CommanderBuilder;

public partial class CommanderBuilderInfoModal : ComponentBase
{
    [Parameter] public bool          Visible  { get; set; }
    [Parameter] public EventCallback OnClose  { get; set; }
}
