using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class RoleTargetEditor : ComponentBase
{
    [Parameter, EditorRequired] public required IReadOnlyDictionary<CardRole, int> CustomIdeal { get; set; }
    [Parameter] public EventCallback<(CardRole Role, object? Value)> OnRoleChanged { get; set; }
    [Parameter] public EventCallback OnReset { get; set; }

    private static readonly CardRole[] RoleDisplayOrder = CardRoleDisplay.DisplayOrder;

    private bool _showEditor = false;
}
