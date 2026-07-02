using EdhDeckBuilder.Core.Cards;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class ColorIdentityPips
{
    [Parameter] public Color Identity { get; set; }

    private static IEnumerable<(string Symbol, string BadgeClass, string Style)> GetPips(Color identity)
    {
        if (identity == Color.None)
            yield return ("C", "badge bg-secondary", "");
        if (identity.HasFlag(Color.White))
            yield return ("W", "badge border", "background:#f9fafb;color:#555;");
        if (identity.HasFlag(Color.Blue))
            yield return ("U", "badge bg-primary", "");
        if (identity.HasFlag(Color.Black))
            yield return ("B", "badge bg-dark", "");
        if (identity.HasFlag(Color.Red))
            yield return ("R", "badge bg-danger", "");
        if (identity.HasFlag(Color.Green))
            yield return ("G", "badge bg-success", "");
    }
}
