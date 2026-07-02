# EdhDeckBuilder

An LLM-assisted Commander (EDH) deck builder in C#/.NET, with a Blazor front end.

## Solution layout

```
EdhDeckBuilder.slnx
├── EdhDeckBuilder.Core            # domain model + rules + templates (no external deps)  ← done
├── EdhDeckBuilder.Infrastructure  # Scryfall + EDHREC clients, local card store         ← done
├── EdhDeckBuilder.Agent           # Anthropic SDK, fill engine, LLM seam, pipeline      ← done
└── EdhDeckBuilder.Web             # Blazor Web App (the visual, grouped deck view)       ← done
```

Dependency direction points inward: everything references `Core`, `Core` references nothing.
That keeps the domain and the EDH rules pure, fast, and unit-testable without touching the
network or the LLM.

## What's in Core

All pure domain code, no external dependencies.

- **`Cards/Color.cs`** — colors as a `[Flags]` enum, so a color identity is a bit set and the
  central legality check (`identity.IsWithin(commander)`) is one bitwise operation.
- **`Cards/Card.cs`** — immutable `Card` record. Color identity and Commander legality are stored
  as given by Scryfall, never recomputed from mana cost or text.
- **`Cards/CardRole.cs`** — the functional buckets (Land, Ramp, CardAdvantage, TargetedDisruption,
  MassDisruption, Tutor, Protection, Payoff, Plan, Synergy) the fill engine and UI group by.
- **`Cards/RoleProfile.cs`** — the multi-role overlap model: a `Primary` role plus `Secondary`
  contributions, each tagged with a `RoleRelation` (`Always` / `Modal` / `Transform`) and a
  coverage weight. A single card can count toward multiple role targets simultaneously.
- **`Decks/Deck.cs`** — `Deck` + `DeckSlot`. Supports one or two commanders; color identity is
  their union. `GroupByRole()` (by primary role) feeds the visual layout; `CoverageByRole()` is the
  overlap-aware accounting that can legitimately sum past 99.
- **`Decks/DeckTemplate.cs`** — soft per-role targets (the "stable deck" ratios) with a `Balanced` preset.
- **`Decks/Archetype.cs`** — archetypes as composable *deltas* over the baseline (`ArchetypeProfile`).
  Four archetypes: Control, Aggro, Combo, Midrange. These describe *how* the deck wins (strategic posture).
- **`Decks/Theme.cs`** — themes as composable deltas on the same baseline (`ThemeProfile`), independent
  of archetypes. Six themes: BigMana, Aristocrats, Voltron, Tokens, Lifegain, Reanimator. These describe
  *what* the deck does mechanically. A theme can pair with any archetype.
- **`Decks/TemplateResolver.cs`** — blends weighted archetypes, themes, and a bracket over a baseline
  and normalizes the result. The deterministic half of the build; the LLM only chooses the weights.
- **`Rules/Rules.cs`** — composable `IDeckRule`s and a `DeckValidator` enforcing the hard format
  rules (100 cards, singleton, color identity, legal commander, banlist).
- **`Abstractions/Abstractions.cs`** — the seams (`ICardRepository`, `ISuggestionSource`) that
  Infrastructure and Agent implement.

## Agent pipeline

The `EdhDeckBuilder.Agent` layer orchestrates a **staged, deterministic pipeline** that consults
the LLM at exactly two fixed points. Everything else is pure code.

```
resolve template
    ↓
gather pool        ← ISuggestionSource (Infrastructure / EDHREC)
    ↓
filter pool        ← color identity, legality, singleton, exclude commanders
    ↓
classify commanders  ← LLM call 1 (ILlmClassifier / claude-haiku-4-5, batch)
    ↓
compute net targets  ← commander coverage subtracted at 1.5× weight
    ↓
classify pool        ← LLM call 1 continued (50-card batches, cached by OracleId)
    ↓
FillEngine           ← greedy fill (scarce→abundant) + reconciliation swap loop
    ↓
ColorFixingPass      ← swap basics for non-basics ranked by pip-demand score
    ↓
RepairIllegalCards   ← deterministic: swap any CI violator with best legal alternative
    ↓
DistributeBasics     ← proportional by pip demand; last color absorbs rounding
    ↓
RepairEngine.Assemble → DeckBuildResult
```

### LLM consultation points

**Classification** (`LlmClassifier` — `claude-haiku-4-5-20251001`, temperature 0.1)  
Input: a batch of `CardCandidate`s + commander context.  
Output: `[{oracleId, primaryRole, secondaryRoles, landCredit}]`.  
Results are cached globally by `OracleId` except for `Plan`, `Synergy`, and `Payoff`, which are
commander-dependent and re-classified per build.

**Selection** (`LlmSelector` — user-selected model, default `claude-haiku-4-5-20251001`, temperature 0.6)  
Input: role-filtered classified pool + current build state + soft constraints.  
Output: `[{oracleId, rank, rationale}]` — ranked picks only; the fill engine decides count.  
The model is configurable per session via the settings UI (Haiku / Sonnet 5 / Opus 4.8).

Both calls use a forced tool call (`ToolChoiceTool`, `Tool.Strict = true`) for guaranteed
structured output. The model can never emit a card name not in the input batch; all responses
are filtered against a whitelist of known oracle ids.

### Slot accounting invariant

At all times during the fill: `BuildState.Committed.Count + BuildState.BasicCount = 99` (or 98
for a partner pair). Utility lands take a basic land slot; MDFCs take a spell slot but also
reduce the basic count by `LandCredit` (0–1). ColorFixingPass swaps one basic for one non-basic
per iteration, so the total stays constant.

### DI registration

```csharp
// In Program.cs / Startup.cs (after AddDataProtection and AddInfrastructure):
builder.Services.AddAgent();
```

The API key is supplied per-user via the settings UI (BYOK). In development, you can pre-populate
it by setting `Anthropic:ApiKey` in user-secrets or the `ANTHROPIC_API_KEY` environment variable —
`SessionApiKeyProvider` reads it on construction so the UI shows "Connected" automatically.

## Getting it running

```bash
# Clone, then restore and build:
dotnet restore
dotnet build

# Run all 271 tests:
dotnet test Tests

# Run the app:
dotnet run --project EdhDeckBuilder.Web
```

On first launch, paste your Anthropic API key in the "Connect your Anthropic API key" card.
The key is held server-side for the session and optionally saved as an encrypted browser cookie.

For development, skip the UI step by setting the key in user-secrets:

```bash
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project EdhDeckBuilder.Web
```

Requires the .NET 10 SDK.

## What's in Web

The Blazor front end (`EdhDeckBuilder.Web`). Pages and components aside, the non-obvious
architectural pieces in `Services/` are:

- **`DeckReportExporter`** — generates the markdown build report. Accepts the full
  `DeckBuildResult` plus build metadata (commanders, archetype weights, themes, bracket,
  budget, date) and returns a self-contained string ready to download.

- **`DeckResultStorage`** — serializes/deserializes `StoredDeckResult` to/from JSON for
  localStorage. Uses `JsonStringEnumConverter` so enum dictionary keys (`CardRole`,
  `Archetype`) round-trip as strings rather than integers. Also owns the localStorage key
  scheme (`edh-deck-{id}`) and the saved-result limit:

  ```csharp
  // EdhDeckBuilder.Web/Services/DeckResultStorage.cs
  public const int DefaultMaxSavedResults = 3;
  ```

  To wire this to a subscription tier: resolve the limit from wherever tiers are stored
  (e.g. `_subscriptionService.GetDeckResultLimit(userId)`) and pass it to
  `JS.InvokeVoidAsync("saveDeckResult", key, json, resolvedLimit)` in `GuidedTab` and
  `CustomTab`. The JavaScript function already accepts it as a parameter — no JS changes
  needed.

- **`DeckResultStore`** — a singleton in-memory cache (`ConcurrentDictionary<string, StoredDeckResult>`)
  that lets the results page retrieve a just-built deck without any JS interop. The builder
  tabs call `Put(id, stored)` before navigating; `DeckResultsPage` calls `Get(Id)` first and
  only falls back to localStorage if the result is not in memory (i.e. after a page reload).
  This avoids sending the full deck JSON back from browser to server over SignalR on the
  normal navigation path.

- **`StoredDeckResult`** — the serialization DTO. Contains `DeckBuildResult` plus the build
  parameters needed to reproduce the report and the results page header (commanders,
  archetype weights, themes, bracket, budget, build date).

### localStorage persistence

After a successful build, the tab serializes `StoredDeckResult` to JSON, writes it to
localStorage via `saveDeckResult(key, value, maxResults)` in `app.js`, and navigates to
`/results/{id}`. The JavaScript function maintains an ordered index (`edh-deck-index`)
and evicts the oldest entries when the count exceeds `maxResults`, keeping localStorage
bounded.

Reading a large JSON back from browser to server over SignalR is limited by
`MaximumReceiveMessageSize` (default: 32 KB). The app raises this to 5 MB in `Program.cs`
so the page-reload path (which must fetch the JSON from localStorage) works for realistic
deck sizes (~300–500 KB serialized):

```csharp
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 5 * 1024 * 1024);
```

---

See `HOW_IT_WORKS.md` for a plain-language explanation of what the tool does for you, what you
need to provide, and where your judgment still matters — written for someone using the deck builder,
not someone building it.

See `TODO/TODO.md` for deferred work and `TODO/AGENT_PRINCIPLES.md` for the design rationale behind the agent layer.
