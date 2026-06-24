namespace EdhDeckBuilder.Infrastructure.Edhrec.Dto;

internal sealed class EdhrecPage
{
    public EdhrecContainer? Container { get; init; }
}

internal sealed class EdhrecContainer
{
    public EdhrecJsonDict? JsonDict { get; init; }
}

internal sealed class EdhrecJsonDict
{
    public List<EdhrecCardlist> Cardlists { get; init; } = [];
}

internal sealed class EdhrecCardlist
{
    public string Header { get; init; } = "";
    public string? Tag { get; init; }
    public List<EdhrecCardView> Cardviews { get; init; } = [];
}

internal sealed class EdhrecCardView
{
    public string Name { get; init; } = "";
    public int NumDecks { get; init; }
    public int PotentialDecks { get; init; }
    public double Synergy { get; init; }
    public string? Label { get; init; }
}
