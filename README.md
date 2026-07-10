# EdhDeckBuilder

An LLM-assisted Commander (EDH) deck builder in C#/.NET, with a Blazor front end. Supports both
Anthropic Claude (via direct HTTP — `ClaudeHttpLlmClient`) and Google Gemini (via a custom REST
client — `GeminiHttpLlmClient`) as the LLM backend, chosen per user session.

## Solution layout

```
EdhDeckBuilder.slnx
├── EdhDeckBuilder.Core            # domain model + rules + templates (no external deps)  ← done
├── EdhDeckBuilder.Infrastructure  # Scryfall + EDHREC clients, local card store         ← done
├── EdhDeckBuilder.Agent           # Anthropic + Gemini adapters, fill engine, pipeline  ← done
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
classify pool        ← LLM call 1 continued (30-card batches, cached by OracleId)
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

**Classification** (`ILlmClassifier`, temperature 0.1, 30-card batches)  
Input: all candidate pool cards + commander context.  
Output: `[{oracleId, primaryRole, secondaryRoles, landCredit, reasoning?}]`.  
Results are cached globally by `OracleId` except for `Plan`, `Synergy`, and `Payoff`, which are
commander-dependent and re-classified per build. The `reasoning` field is optional (controlled by
`EnableClassificationReasoning` config; disabled in Production to save output tokens).

**Selection** (`ICardSelector`, temperature 0.6, per-role call)  
Input: role-filtered classified pool + current build state + soft constraints.  
Output: `[{oracleId, rank, rationale}]` — ranked picks only; the fill engine decides count.  
The model is configurable per session via the settings UI.

Both calls use forced structured output (Anthropic: `tool_choice: {type: "tool", name: "..."}` in
the raw HTTP body; Gemini: `responseSchema` with `responseMimeType: "application/json"`). The
model can never emit a card name not in the input batch; all responses are filtered against a
whitelist of known oracle ids.

### Provider adapters

Three **provider-agnostic** adapters in `EdhDeckBuilder.Agent/Llm/` hold all business logic:

| Adapter | Interface | What it does |
|---|---|---|
| `LlmClassifier` | `ILlmClassifier` | Classifies cards in 30-card batches, caches results |
| `LlmSelector` | `ICardSelector` | Ranks candidates per role for the fill engine |
| `LlmCommanderSelector` | `ICommanderSelector` | Ranks commander candidates for Discovery |

Each adapter calls `ILlmClientFactory.CreateForCurrentUser()` to get the active provider's
HTTP client, then formats a provider-agnostic `LlmRequest` and parses the `LlmResponse`. Two
concrete `ILlmClient` implementations handle the actual wire format:

**`Llm/Claude/ClaudeHttpLlmClient`** — posts directly to `api.anthropic.com/v1/messages` with
`x-api-key` and `anthropic-version` headers. No Anthropic C# SDK; pure `HttpClient`. Handles
retries (429/50x with `Retry-After`-aware backoff), maps 401/403 to `ApiKeyRejectedException`,
and serializes prompt-cache breakpoints when `LlmRequest.EnableCaching = true` (system prompt
is marked ephemeral; cache fires when the system+tool token count exceeds the per-model minimum).
Classification always runs on `claude-haiku-4-5-20251001`; selection uses the user's chosen
model (Haiku / Sonnet 5 / Opus 4.8).

**`Llm/Gemini/GeminiHttpLlmClient`** — wraps `GeminiRestClient`, which posts directly to
`https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent` with a
`x-goog-api-key` header. No OpenAI SDK. Structured output is requested via
`generationConfig.responseSchema` (Gemini's OpenAPI 3.0 subset). The client simulates a
`LlmToolUseBlock` in the response so all three adapters can treat both providers uniformly.
Both classifier and selector run on the user's chosen Gemini model.

Supporting pieces on the Gemini path:

- **`GeminiRateLimiter`** (Scoped per Blazor circuit) enforces a minimum 1050 ms spacing between
  calls to stay under free-tier RPM ceilings (as low as 5 RPM on some models).
- **`GeminiRestClient`** retries transient failures (429, 502, 503, 504) up to 3 times with
  `Retry-After`-aware backoff. Non-transient errors (401/403 → `ApiKeyRejectedException`,
  MAX_TOKENS truncation → returned as null payload) are surfaced cleanly with Google's structured
  error body parsed into the exception message so `RESOURCE_EXHAUSTED` cases (billing-gated
  models on free tier) are diagnosable at first glance.
- **`GeminiSchemas`** builds the response schema in Gemini's dialect — uppercase types
  (`OBJECT` / `ARRAY` / `STRING`), `format: enum` for constrained strings, and `propertyOrdering`
  set so reasoning fields (e.g. `rationale`) precede the answer they justify. This measurably
  improves ranking quality on smaller models.

### Cost accounting

`Instrumentation/ModelPricing.cs` holds per-model paid-tier USD rates per 1M tokens (Anthropic:
Haiku 4.5, Sonnet 5, Opus 4.8; Gemini: 2.5/2.0 Flash, Flash Lite, 3.x series). `UsageTracker`
uses it for both per-call rows and the summary total — a mixed-provider run tallies correctly.
Free-tier Gemini usage still reports a cost figure: it's the "what you'd pay on paid tier"
estimate. When a new model is added to a picker, add its row to `ModelPricing.Prices` in the
same change; unknown models silently report $0.

`IUsageTrackerAware` is a marker interface implemented by all three adapters. `DeckBuilder` and
`CommanderDiscovery` dispatch through it — no type-specific `is LlmXxx` branches.

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

API keys are supplied per-user via the settings UI (BYOK). In development, you can pre-populate
any of the three provider keys via user-secrets or environment variables — `SessionApiKeyProvider`
reads them on construction so the UI shows "Connected" automatically:

| Provider | Config key |
|---|---|
| Anthropic | `Anthropic:ApiKey` |
| Google Gemini | `Google:ApiKey` |
| OpenAI | `OpenAI:ApiKey` |

The startup provider can be selected via `Provider:Default` (`Anthropic` / `Google` /
`OpenAI`). **Note:** the provider is also persisted per-user in a browser cookie, so once
you connect through the UI the cookie wins on subsequent loads. To force a config change to take
effect, clear the cookie or use an incognito window.

## Getting it running

```bash
# Clone, then restore and build:
dotnet restore
dotnet build

# Run all 327 tests:
dotnet test Tests

# Run the app:
dotnet run --project EdhDeckBuilder.Web
```

On first launch, pick a provider and paste its API key in the "Connect your API key" card.
The key is held server-side for the session and optionally saved as an encrypted browser cookie.

For development, skip the UI step by setting a key in user-secrets:

```bash
# Anthropic
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project EdhDeckBuilder.Web
# Or Google Gemini
dotnet user-secrets set "Google:ApiKey" "AIza..." --project EdhDeckBuilder.Web
dotnet user-secrets set "Provider:Default" "Google" --project EdhDeckBuilder.Web
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
  `JS.InvokeVoidAsync("saveDeckResult", key, json, resolvedLimit)` in
  `GuidedCommanderBuilderTab` and `CustomCommanderBuilderTab`. The JavaScript function
  already accepts it as a parameter — no JS changes needed.

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

### Component organization

Blazor components in `EdhDeckBuilder.Web/Components/` are organized into logical subfolders:

```
Components/
├── Pages/
│   ├── CommanderBuilder/  # CommanderBuilder page + GuidedCommanderBuilderTab + CustomCommanderBuilderTab
│   ├── Discovery/          # CommanderDiscovery page + GuidedDiscoveryTab + CustomDiscoveryTab
│   └── (other pages)       # DeckResultsPage, Home, etc.
├── Forms/              # Input controls & pickers (ArchetypePicker, BracketPicker, BudgetPicker, ColorIdentityPicker, CommanderPicker, ThemePicker)
├── Shared/             # Reusable UI components (ApiKeySettings, ColorIdentityPips, CommanderSuggestionCard)
├── Results/            # Deck display components (BuildProgress, DeckExportPanel, DeckResults)
├── Layout/             # Navigation chrome (MainLayout, NavMenu, ReconnectModal)
└── _Imports.razor      # Global usings for all components
```

All component code is in `.razor.cs` code-behind files using the `public partial class` pattern — no `@code` blocks in `.razor` markup. Each component inherits from `ComponentBase` (explicit in code-behind) and includes its full namespace declaration.

---

See `HOW_IT_WORKS.md` for a plain-language explanation of what the tool does for you, what you
need to provide, and where your judgment still matters — written for someone using the deck builder,
not someone building it.

See `TODO/TODO.md` for deferred work and `TODO/AGENT_PRINCIPLES.md` for the design rationale behind the agent layer.
