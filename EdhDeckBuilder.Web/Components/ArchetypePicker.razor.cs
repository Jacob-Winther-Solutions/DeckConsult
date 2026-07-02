using EdhDeckBuilder.Core.Decks;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components;

public partial class ArchetypePicker
{
    [Parameter] public EventCallback<IReadOnlyDictionary<Archetype, double>> OnChanged { get; set; }
    [Parameter] public IReadOnlyDictionary<Archetype, double>? InitialArchetypeWeights { get; set; }

    private readonly Dictionary<Archetype, double> _weights = new();

    internal IReadOnlyDictionary<Archetype, double> Weights => _weights;

    protected override void OnInitialized()
    {
        if (InitialArchetypeWeights is not null)
        {
            foreach (var (archetype, weight) in InitialArchetypeWeights)
            {
                _weights[archetype] = weight;
            }
        }
    }

    private async Task ToggleArchetype(Archetype a)
    {
        if (_weights.ContainsKey(a)) _weights.Remove(a);
        else _weights[a] = 1.0;
        await NotifyChanged();
    }

    private async Task SetWeight(Archetype a, double w)
    {
        _weights[a] = w;
        await NotifyChanged();
    }

    private async Task NotifyChanged() => await OnChanged.InvokeAsync(_weights);
}
