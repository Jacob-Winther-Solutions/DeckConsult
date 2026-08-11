using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class RoleHelpTip : ComponentBase
{
    [Parameter] public string Description { get; set; } = "";
}
