# TCGplayer Mass Entry — Affiliate "Buy This Deck" Linking

> **Read this first.** This is a design spec produced from a planning conversation.
> The author of this spec does **not** have access to the current state of the
> codebase. **Adapt everything below to the real implementation**: namespaces,
> DI conventions, the actual deck/card domain types, and the Blazor hosting model.
> Treat all code blocks as *illustrative drafts*, not drop-in files. Where this
> spec names a type that doesn't exist or has a different shape in the repo, map
> to the real one rather than inventing a parallel structure. Do **not** redesign
> settled architecture (e.g. the `Core` purity boundary) to fit these snippets.

## Purpose

Add a monetization surface to the deck builder: a "Buy this deck on TCGplayer"
action on a finished deck. Clicking it sends the full decklist into TCGplayer's
**Mass Entry** cart tool, tagged with our affiliate code so referred purchases
earn commission. Mechanically this is a URL/form builder — no TCGplayer API
account or server-to-server call is required for the affiliate link itself.

## Verified external facts (as of mid-2026 — re-verify before launch)

These are facts about TCGplayer's program that you cannot derive from the code.
They drive the design; do not silently change them.

- **Program runs through Impact.** You must **apply and be accepted** before any
  link earns. The affiliate code comes from the Impact dashboard, not from us.
- **Commission:** ~**3.5% per sale**, applied to all products.
- **Attribution:** **first-click, 48-hour window**, and the **whole cart** is
  credited (not just the linked card). So sending a user in for one deck and
  having them add more still earns on the full basket.
- **Payment:** ~45 days after the end of each month.
- **Endpoints (these differ for GET vs POST):**
  - GET: `https://www.tcgplayer.com/massentry`
  - POST: `https://api.tcgplayer.com/massentry`
- **`c` parameter format:** entries of `"<qty> <name>"` joined by a literal `||`
  separator. Spaces are URL-encoded as `%20`; the `||` separators stay literal.
  Each entry may optionally include a set code and collector number, e.g.
  `4 Lightning Bolt [M10] 146`. For our purposes name + quantity is enough.
- **Product line:** pass `productline=Magic`.
- **Affiliate query params:** `partner=<CODE>`, `utm_campaign=affiliate`,
  `utm_source=<CODE>`, and optionally `utm_medium=<segment>`.
  > ⚠️ Confirm the exact param name/value against the Impact dashboard once
  > accepted — Impact sometimes hands you a wrapped link or a specific partner ID.
- **GET has a URL-length limit.** A ~100-card Commander list can exceed the safe
  ~2 KB mark (works in modern browsers up to ~8 KB, but is borderline). For full
  decks, **prefer POST**. The POST endpoint returns a **303 redirect** to the
  cart; the posted data is only valid for ~5 minutes (irrelevant for us, since
  the browser follows the redirect immediately).
- **Why a native HTML form for POST, not `fetch`:** a JS `fetch` would hit CORS
  and could not follow the cross-origin 303 to the cart. A real `<form>` submit
  navigates the browser, so the redirect resolves and the cart loads. Use a form.

## Legal caveat to surface, not to implement around

Before enabling *any* monetization, confirm the current **Wizards of the Coast
Fan Content Policy** permits it for a tool like this. This is a product/legal
decision, not a coding one — flag it to the project owner; do not assume.

## Proposed design

Lives in the **Web** project (presentation/integration concern). Keep `Core`
pure — no TCGplayer types leak into the domain. Map the finished deck to a flat
list of purchasable lines **at the call site**, so the builder stays domain-agnostic.

### Builder + options (adapt namespace to repo convention)

```csharp
namespace EdhDeckBuilder.Web.Integrations.TcgPlayer; // adapt to real convention

/// One purchasable line: a card and how many copies
/// (1 for singletons, N for basic lands).
public readonly record struct CartLine(string CardName, int Quantity);

public sealed class TcgPlayerLinkOptions
{
    /// TCGplayer/Impact affiliate code. Null => plain link, no commission.
    public string? AffiliateCode { get; init; }
    /// Optional UTM medium to segment click sources in the Impact dashboard.
    public string? Medium { get; init; } = "deckbuilder";
}

public readonly record struct MassEntryPostForm(string ActionUrl, string CardListValue);

public sealed class TcgPlayerMassEntryLinkBuilder(TcgPlayerLinkOptions options)
{
    private const string GetUrl  = "https://www.tcgplayer.com/massentry";
    private const string PostUrl = "https://api.tcgplayer.com/massentry";

    /// GET link. Fine for single cards / small lists / previews.
    /// May truncate on full decks — use BuildPostForm for those.
    public string BuildGetUrl(IEnumerable<CartLine> lines)
    {
        var list = JoinList(lines);
        // Encode the whole value, then restore the "||" separators
        // (card names never contain '|').
        var encodedC = Uri.EscapeDataString(list).Replace("%7C%7C", "||");

        var sb = new StringBuilder(GetUrl)
            .Append("?productline=Magic")
            .Append("&c=").Append(encodedC)
            .Append(BuildAffiliateQuery(prefixWithAmp: true));
        return sb.ToString();
    }

    /// Data for an HTML form POST. Use for full decks — no URL-length limit.
    /// CardListValue is RAW; the browser form-encodes it on submit.
    public MassEntryPostForm BuildPostForm(IEnumerable<CartLine> lines) =>
        new(PostUrl + BuildAffiliateQuery(prefixWithAmp: false), JoinList(lines));

    private static string JoinList(IEnumerable<CartLine> lines) =>
        string.Join("||",
            lines.Where(l => l.Quantity > 0 && !string.IsNullOrWhiteSpace(l.CardName))
                 .Select(l => $"{l.Quantity} {l.CardName.Trim()}"));

    private string BuildAffiliateQuery(bool prefixWithAmp)
    {
        if (string.IsNullOrWhiteSpace(options.AffiliateCode)) return string.Empty;

        var code = Uri.EscapeDataString(options.AffiliateCode);
        var sb = new StringBuilder(prefixWithAmp ? "&" : "?")
            .Append("partner=").Append(code)
            .Append("&utm_campaign=affiliate")
            .Append("&utm_source=").Append(code);
        if (!string.IsNullOrWhiteSpace(options.Medium))
            sb.Append("&utm_medium=").Append(Uri.EscapeDataString(options.Medium));
        return sb.ToString();
    }
}
```

### Buy button component (Blazor)

```razor
@* BuyDeckButton.razor — class names are placeholders; style later *@
@inject TcgPlayerMassEntryLinkBuilder LinkBuilder

@if (_form is { } form)
{
    @* data-enhance="false" prevents Blazor enhanced-nav from intercepting.
       target=_blank opens the cart in a new tab. *@
    <form method="post" action="@form.ActionUrl"
          target="_blank" rel="noopener" data-enhance="false">
        <input type="hidden" name="productline" value="Magic" />
        <input type="hidden" name="c" value="@form.CardListValue" />
        <button type="submit">Buy this deck on TCGplayer</button>
    </form>
}

@code {
    [Parameter, EditorRequired] public IReadOnlyList<CartLine> Lines { get; set; } = [];
    private MassEntryPostForm? _form;
    protected override void OnParametersSet() => _form = LinkBuilder.BuildPostForm(Lines);
}
```

Razor auto-encodes the hidden `value` attribute, so apostrophes/ampersands in
card names (e.g. `Urza's`, `Wear // Tear`) are handled.

### Mapping the deck → CartLine (call site)

This is where the **real domain types** plug in. The conversation referenced a
`BuildState`/deck aggregate, but **verify the actual type and member names**.
Conceptually: commander(s) at quantity 1, nonland spells at 1 (singleton),
basic lands at their real counts. Produce `IReadOnlyList<CartLine>` and pass to
`<BuyDeckButton Lines="..." />`.

### DI registration (adapt to existing setup)

```csharp
services.AddSingleton(new TcgPlayerLinkOptions { AffiliateCode = /* from config */ });
services.AddScoped<TcgPlayerMassEntryLinkBuilder>();
```

Pull `AffiliateCode` from configuration/secrets, not a literal.

## Guardrails for Claude Code

- **Adapt, don't transplant.** Namespaces, DI lifetimes, and the deck/card types
  shown here are guesses from a conversation. Match the repository's real shapes.
- **Keep `Core` pure.** No TCGplayer types in the domain project. The mapping from
  the deck aggregate to `CartLine` happens in Web (or a thin integration layer).
- **Don't hardcode the affiliate code.** Configuration/secret only.
- **Encoding is load-bearing.** Preserve the "encode whole `c`, then restore `||`"
  approach for GET, and the raw-value-in-hidden-input approach for POST. If you
  change it, test against a real card with a comma and an apostrophe in the name.
- **Prefer POST for full decks**; keep GET for single-card/preview links.
- **Confirm the affiliate params** against the Impact dashboard before relying on
  commission tracking; the `partner`/utm form is documented but Impact may differ.

## Open questions / follow-ups

- Multi-retailer support later (Card Kingdom etc.) — keep the builder interface
  shaped so a second provider can slot in.
- Whether to also expose a plain text decklist export for users who'd rather
  paste into Mass Entry themselves.
- A POST variant is unnecessary for single-card links inside the UI; decide where
  GET vs POST is used per surface.
