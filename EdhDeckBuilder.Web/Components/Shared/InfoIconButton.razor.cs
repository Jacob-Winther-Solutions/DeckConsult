using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class InfoIconButton : ComponentBase
{
    [Parameter] public EventCallback OnClick { get; set; }
}
