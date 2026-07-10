# CLAUDE.md

Project context and working instructions for Claude Code. Read this, `README.md`, and
`TODO/AGENT_PRINCIPLES.md` before doing anything in the Agent layer.

**Document maintenance:** when `README.md` is updated (new capabilities, changed limitations,
new pipeline stages), also review `HOW_IT_WORKS.md` and keep it in sync. `HOW_IT_WORKS.md`
is the user-facing counterpart to `README.md` — it describes the same system from the
perspective of someone using the deck builder rather than building it.

## What this is

An LLM-assisted Magic: the Gathering Commander (EDH) deck builder in C#/.NET 10, with a Blazor
front end. The user drives it in natural language; an agent builds and validates decks against
a pure domain core. Three LLM providers are wired: **Anthropic Claude** (via `ClaudeHttpLlmClient`),
**OpenAI** (via `OpenAiHttpLlmClient`), and **Google Gemini** (via `GeminiRestClient`). All three
implement `ILlmClient` so the three provider-agnostic adapters (`LlmClassifier`, `LlmSelector`,
`LlmCommanderSelector`) are shared. Runtime dispatch by `AiProvider` on the per-circuit
`SessionApiKeyProvider`.
`README.md` documents the architecture; `TODO/TODO.md` tracks deferred work;
`TODO/AGENT_PRINCIPLES.md` is the design rationale document for the Agent layer.

## Current state

All four projects exist and compile. Run `dotnet test Tests` — 337 tests, all green.

| Project | Status |
|---|---|
| `EdhDeckBuilder.Core` | Done — domain model, rules, templates, archetypes, themes, brackets |
| `EdhDeckBuilder.Infrastructure` | Done — Scryfall bulk client, EDHREC client, `SuggestionSource` |
| `EdhDeckBuilder.Agent` | Done — fill engine, LLM seam, pipeline, BYOK (`Authentication/`), DI |
| `EdhDeckBuilder.Web` | Done — full Blazor UI: commander search, deck views, budget, export, BYOK UI |

See `TODO/TODO.md` for remaining work (Commander Discovery, Deployment, etc.).

## Agent layer — how it works

`Pipeline/DeckBuilder.cs` is the entry point (`IDeckBuilder.BuildAsync`). It runs a 10-stage
staged pipeline; the LLM is consulted at exactly two fixed points:

1. **Classification** — `ILlmClassifier` / `LlmClassifier` (temperature 0.1, batched at 30
   cards, forced structured output, prompt caching enabled). Assigns `CardRole` + secondary
   overlaps. Results cached globally by `OracleId` except `Plan`, `Synergy`, and `Payoff`,
   which are re-classified per build.
   - Anthropic path: `claude-haiku-4-5-20251001` regardless of user selection (fast + cheap).
   - OpenAI path: `gpt-4o-mini` regardless of user selection (fast + cheap).
   - Gemini path: user's selected Gemini model (higher token ceiling: 32 768).
2. **Selection** — `ICardSelector` / `LlmSelector` (temperature 0.6, per-role call, forced
   structured output, prompt caching enabled). Returns a ranked list with per-card rationale.
   The fill engine decides count; the model never outputs counts.
   - Anthropic path: user-selected Claude model, default Haiku.
   - OpenAI path: user-selected OpenAI model, default `gpt-4o-mini`.
   - Gemini path: user's selected Gemini model.

Everything else — fill order, reconciliation, color-fixing, repair, basic distribution — is
deterministic code in `Fill/` and `Pipeline/`.

All open design decisions from `TODO/AGENT_PRINCIPLES.md` are now closed. Do not reopen them
without an explicit user request.

### Provider dispatch

`SessionApiKeyProvider.ActiveProvider` (`Anthropic` / `OpenAI` / `Google`) is set at the
UI level and read at the DI factory lambda in `ServiceCollectionExtensions.AddAgent`. The lambda
is a three-way switch that resolves `ILlmClientFactory` to `ClaudeHttpLlmClientFactory`,
`OpenAiLlmClientFactory`, or `GeminiLlmClientFactory` at scope-resolution time. All three
interfaces (`ILlmClassifier`, `ICardSelector`, `ICommanderSelector`) share the same dispatch
through the factory.

### Gemini path — custom REST client

The Gemini adapters do NOT wrap the OpenAI-compatible endpoint via the OpenAI C# SDK. Instead:

- `GeminiRestClient` posts directly to
  `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent` with a
  `x-goog-api-key` header. Constructed per-call by `GeminiClientFactory` (Scoped) using an
  `HttpClient` from `IHttpClientFactory` (registered via `AddHttpClient<GeminiClientFactory>`).
- `GeminiSchemas` builds the `responseSchema` in Gemini's OpenAPI 3.0 subset — uppercase types
  (`OBJECT` / `ARRAY` / `STRING` / etc.), `format: enum` for constrained strings, and
  `propertyOrdering` set so reasoning fields precede the answer they justify.
- `GeminiRateLimiter` (Scoped, per Blazor circuit) serializes calls with a minimum 1050ms
  spacing. Free-tier RPM ceilings on 2.5 Flash and lite variants are as low as 5 RPM.
- `GeminiRestClient` retries transient failures (429, 502, 503, 504) up to 3 times with
  `Retry-After`-aware backoff. Google's error body (`RESOURCE_EXHAUSTED` + `QuotaFailure`
  details) is parsed and surfaced in the exception message so free-tier `limit: 0` gating is
  diagnosable at first glance.

### Cost accounting

`ModelPricing` (in `Instrumentation/`) is the single source of truth for per-model USD rates
per 1M tokens. `UsageTracker` consults it via `ModelPricing.EstimateCost(modelId, in, out)` for
both per-call rows and the summary aggregate — a mixed-provider run tallies correctly. Unknown
model IDs return the zero rate rather than throwing, so a new model added to a picker without a
matching pricing entry quietly reports $0 until priced.

`IUsageTrackerAware` (marker interface in `Instrumentation/`) is implemented by all six adapters
(three Anthropic + three Gemini). `DeckBuilder` and `CommanderDiscovery` wire the tracker
through it — no type-specific `is LlmXxx` dispatch. Any future provider adapter that implements
the marker will be picked up automatically.

## Guardrails — do not redesign Core

These decisions are intentional. Keep them unless the user explicitly asks to change them.

- **Dependency direction points inward.** Core references nothing external. Infrastructure and
  Agent depend on Core, never the reverse.
- **Scryfall owns the facts.** `Card.ColorIdentity` and `Card.CommanderLegality` are stored as
  Scryfall reports them. Never derive color identity from mana cost or oracle text.
- **Color identity is a `[Flags]` enum**; the subset check is `ColorExtensions.IsWithin`.
- **Two kinds of counting are distinct and both matter:** physical count (`PrimaryRole`, totals
  100 with commanders) vs. role *coverage* (`Deck.CoverageByRole()`, overlap-aware, may exceed 99).
  Don't collapse them.
- **Archetypes and themes are both deltas, resolved deterministically.** Archetypes (*how* the
  deck wins: Control/Aggro/Combo/Midrange) and themes (*what* it does: BigMana/Aristocrats/Voltron/
  Tokens/Lifegain/Reanimator) are independent axes that both compose as adjustments over the same
  baseline. `TemplateResolver` blends them into counts. Don't have the model output raw card counts,
  and don't conflate the two — a theme can pair with any archetype.
- **Hard rules vs. soft templates are separate.** `Rules/` enforces legality; `DeckTemplate`,
  archetypes, and themes only guide construction.
- **The fill engine is the LLM's deterministic consumer.** All judgment is injected upstream via
  `ILlmClassifier` and `ICardSelector`. Fill, reconciliation, and repair are pure code.
- **Slot-accounting invariant:** `BuildState.Committed.Count + BuildState.BasicCount = 99`
  (or 98 for partner pairs) at all times during the fill. Don't break this.

## Guardrails — Agent layer

- **Whitelist rule:** every `OracleId` in an LLM response must echo an id from the input batch.
  Filter before returning to callers. Code that trusts model-returned card names directly is a bug.
- **Forced tool call only:** both LLM calls use `tool_choice: {type: "tool", name: "..."}` with
  the tool schema `required` constraint enforced. Don't switch to plain-text parsing.
- **Classification cache:** `ClassificationCache` is a singleton; Plan, Synergy, and Payoff are
  never served from it. Don't cache them.
- **BYOK — scoped services:** `SessionApiKeyProvider`, `ILlmClientFactory`,
  `IGeminiClientFactory`, `GeminiRateLimiter`, `IKeyTester`, `ILlmClassifier`,
  `ICardSelector`, and `IDeckBuilder` are all Scoped (per Blazor Server circuit).
  `ClassificationCache` is the only Singleton in the Agent layer. Never register a Scoped LLM
  service as Singleton — it would capture the per-circuit key or pacing state.
- **BYOK — HTTP seams:** `ClaudeHttpLlmClientFactory` is the only place that constructs
  `ClaudeHttpLlmClient` (direct HTTP to `api.anthropic.com/v1/messages`). `OpenAiLlmClientFactory`
  is the only place that constructs `OpenAiHttpLlmClient` (direct HTTP to
  `api.openai.com/v1/chat/completions`). `GeminiClientFactory` is the only place that constructs
  `GeminiRestClient`. All three implement `ILlmClientFactory`. No LLM adapter should construct
  an HTTP client itself.
- **BYOK — 401/403 handling:** All three HTTP clients map 401/403 responses to
  `ApiKeyRejectedException`. The UI catches this, calls `Keys.Clear()`, and shows a reconnect
  prompt. Don't swallow it or convert it to a generic build failure.
- **Quota / billing errors:** All three HTTP clients detect quota-exhaustion 429s (body contains
  "quota" or "RESOURCE_EXHAUSTED") and throw `QuotaExceededException` immediately — no retry.
  `KeyTester` catches this and returns a user-facing "Billing limit reached" message. Transient
  rate-limit 429s (no quota keyword) are still retried with backoff.
- **Gemini — free-tier `limit: 0` is a project setting, not our bug.** Some models
  (currently `gemini-2.0-flash*`) return HTTP 429 with `status: RESOURCE_EXHAUSTED` and
  `limit: 0` on projects without billing attached. The full error body (parsed by
  `GeminiRestClient.ExtractErrorMessage`) makes this immediately diagnosable. Don't retry
  around it — the retry loop will just re-fail. The label on `GeminiModels.SelectionModels`
  notes which entries need billing.
- **Gemini — MAX_TOKENS is not a JSON parse error.** `GeminiResponse.GetPayloadText()`
  returns null when `finishReason` is `MAX_TOKENS`, `SAFETY`, `RECITATION`, or `OTHER`. If you
  see truncation in classification, raise `MaxOutputTokens` on the adapter — do not add
  partial-JSON recovery. Current ceilings: classifier 32768, selectors 8192. Gemini bills only
  emitted tokens, not the ceiling.
- **Gemini — rank normalization is centralized.** `CommanderDiscovery.BuildSuggestionsFromResults`
  renumbers ranks to contiguous 1..N after ordering by the model's rank. This lets lite models
  emit ranks like 1, 3, 5 (rank-as-rating) without breaking the display. Don't push
  normalization into the selectors.
- **Usage tracker wiring uses `IUsageTrackerAware`.** All three unified adapters implement it;
  `DeckBuilder.UsageTracker` setter and `CommanderDiscovery.SetUsageTracker` dispatch through
  the marker interface. Don't add `is LlmXxx` / `is GeminiXxx` branches — any new provider that
  implements `IUsageTrackerAware` is wired automatically.
- **Cost accounting goes through `ModelPricing`.** `UsageTracker.GetSummary` and `FormatTable`
  call `ModelPricing.EstimateCost(modelId, in, out)` — no hardcoded rates. When a new model is
  added to a picker (`GeminiModels.SelectionModels` / `ClaudeModels.SelectionModels`), add a
  corresponding row to `ModelPricing.Prices` in the same change; unknown models silently report
  $0 which will surface as a suspicious total.
- **Temperature — model-gated (Anthropic):** `ClaudeHttpLlmClient.ModelSupportsTemperature`
  controls whether the `temperature` field is included in the request. Haiku 4.5 and `claude-3-*`
  variants accept it; Sonnet 5 and Opus 4.8 reject it with HTTP 400. Do not send temperature for
  newly added models until confirmed — just omit it and let the model default.
- **Temperature — model-gated (OpenAI):** `OpenAiHttpLlmClient.IsReasoningModel` gates
  temperature for o-series models (`o1`, `o3`, `o4-*`). These models also use
  `max_completion_tokens` instead of `max_tokens`. Any future o-series model should be covered
  by the existing prefix check; verify before adding gpt-* models to the reasoning gate.
- **Prompt caching — implemented:** `ClaudeHttpLlmClient.BuildRequestJson` serializes the system
  prompt as a structured content block and marks the last tool definition with
  `"cache_control": {"type": "ephemeral"}` when `LlmRequest.EnableCaching = true`. All three
  adapters set this flag. Caching only fires when the combined system+tool tokens exceed
  Anthropic's per-model minimum (~1024 tokens for Sonnet/Opus, ~2048 for Haiku). Verify by
  checking `CacheCreationInputTokens` / `CacheReadInputTokens` in the usage summary after a
  second build run — they will be > 0 when caching is active. The Gemini path ignores
  `EnableCaching` (Gemini uses a different caching mechanism not yet integrated).
  **Known limitation:** classification batches (Haiku) show CacheCreate = 0 even though the
  system+tool prefix exceeds 2048 tokens. Root cause is unclear — Haiku 4.5 may have a higher
  minimum or treat the two-breakpoint prefix differently. The selector (Sonnet 5) caches
  correctly at the system-prompt breakpoint. Classification caching is deferred pending further
  investigation.

## Conventions

- Modern C#: records for data, file-scoped namespaces, nullable enabled, immutability by default.
- **Razor components use code-behind files.** All `@code` blocks in `.razor` files must be extracted
  to corresponding `.razor.cs` files using the `public partial class` pattern. No code blocks directly
  in Razor markup.
- Implement interfaces from `Core/Abstractions/` in the outer layers; don't add new abstractions
  to Core without a good reason.
- Keep secrets (API keys — Anthropic, Google, OpenAI) out of source — use user-secrets or
  environment variables (`Anthropic:ApiKey`, `Google:ApiKey`, `OpenAI:ApiKey`,
  `Provider:Default`).
- Tests live in `Tests/`. No mocking libraries — manual mocks only (the project has no Moq/NSubstitute
  reference). Keep tests fast: LLM calls must be behind the `ILlmClassifier`/`ICardSelector`
  interfaces and replaced with mocks in tests.
- **Before returning control to the user:** always run `dotnet test Tests` (which also builds).
  All tests must pass and the build must be error-free. Fix any failures before responding.
- **Git commits are user's responsibility.** Never commit changes. Stage and test, but leave the
  final commit decision to Jacob. Committing work is a deliberate human action, not an automation step.

## TODO.md maintenance pattern

Structure `TODO.md` as follows:

1. **Investigations** — bugs requiring diagnosis (current state, root-cause hypothesis, next steps).
2. **Feature sections** — one per major feature, with:
   - Brief description of what it accomplishes.
   - Implementation tasks (both complete `[x]` and incomplete `[ ]`).
   - Owner decisions/open questions (if applicable).
3. **Partially deferred features** — features with core implementation done; edge cases or optional enhancements deferred as subsections.
4. **Multi-format support** — format-specific work (Brawl, Duel Commander, Pauper EDH, etc.).
5. **Potential upgrades** — nice-to-have features and conveniences (no blocking path).
6. **Additional card sources** — new data providers (Commander Spellbook, TopDeck.gg, EDHREC extensions, etc.).
7. **Infrastructure** — deployment, caching, data refresh, etc.
8. **Summary of completed work** — a brief bulleted recap of all finished layers (Core, Infrastructure, Agent, Web, Tests).

When features graduate from "in progress" to "done":
- Summarize them into the "Summary of completed work" section.
- Remove their old task checklist from the feature sections.
- Move any deferred edge cases or stretch items into **Partially deferred features** or **Potential upgrades** as appropriate.
- Small deferred items (temperature audit, subscription tier limits, etc.) belong in **Partially deferred features** under a subsection per feature area they improve.
