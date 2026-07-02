using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Results;

public partial class BuildProgress : ComponentBase
{
    [Parameter, EditorRequired] public required IReadOnlyList<string> AllStages { get; set; }
    [Parameter] public string? CurrentStage { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<string> CompletedStages { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
}
