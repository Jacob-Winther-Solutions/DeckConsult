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

internal sealed class EdhrecPartnerCardView
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Sanitized { get; init; } = "";
    public string Url { get; init; } = "";
    public int Inclusion { get; init; }
    public int NumDecks { get; init; }
}

internal sealed class EdhrecPartnerCardlist
{
    public string Header { get; init; } = "";
    public string? Tag { get; init; }
    public List<EdhrecPartnerCardView> Cardviews { get; init; } = [];
}

internal sealed class EdhrecPartnerJsonDict
{
    public List<EdhrecPartnerCardlist> Cardlists { get; init; } = [];
}

internal sealed class EdhrecPartnerContainer
{
    public EdhrecPartnerJsonDict? JsonDict { get; init; }
}

internal sealed class EdhrecPartnerPage
{
    public EdhrecPartnerContainer? Container { get; init; }
}
