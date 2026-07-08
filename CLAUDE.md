# CLAUDE.md

Project context and working instructions for Claude Code. Read this, `README.md`, and
`TODO/AGENT_PRINCIPLES.md` before doing anything in the Agent layer.

**Document maintenance:** when `README.md` is updated (new capabilities, changed limitations,
new pipeline stages), also review `HOW_IT_WORKS.md` and keep it in sync. `HOW_IT_WORKS.md`
is the user-facing counterpart to `README.md` — it describes the same system from the
perspective of someone using the deck builder rather than building it.

## What this is

An LLM-assisted Magic: the Gathering Commander (EDH) deck builder in C#/.NET 10, with a Blazor
front end. The user drives it in natural language; an agent (Anthropic C# SDK) builds and validates
decks using a pure domain core. `README.md` documents the architecture; `TODO/TODO.md` tracks deferred
work; `TODO/AGENT_PRINCIPLES.md` is the design rationale document for the Agent layer.

## Current state

All four projects exist and compile. Run `dotnet test Tests` — 281 tests, all green.

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

1. **Classification** — `LlmClassifier` (`claude-haiku-4-5-20251001`, temperature 0.1, batched,
   forced tool call). Assigns `CardRole` + secondary overlaps. Results cached globally by
   `OracleId` except `Plan`, `Synergy`, and `Payoff`, which are re-classified per build.
   Always uses Haiku regardless of the user's model selection.
2. **Selection** — `LlmSelector` (user-selected model via `IClaudeClientFactory.SelectionModel`,
   default `claude-haiku-4-5-20251001`, temperature 0.6, per-role call, forced tool call).
   Returns a ranked list with per-card rationale. The fill engine decides count; the model never
   outputs counts.

Everything else — fill order, reconciliation, color-fixing, repair, basic distribution — is
deterministic code in `Fill/` and `Pipeline/`.

All open design decisions from `TODO/AGENT_PRINCIPLES.md` are now closed. Do not reopen them
without an explicit user request.

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
- **Forced tool call only:** both LLM calls use `ToolChoiceTool { Name = "..." }` with
  `Tool.Strict = true`. Don't switch to plain-text parsing.
- **Classification cache:** `ClassificationCache` is a singleton; Plan, Synergy, and Payoff are
  never served from it. Don't cache them.
- **BYOK — scoped services:** `SessionApiKeyProvider`, `IClaudeClientFactory`, `IClaudeKeyTester`,
  `ILlmClassifier`, `ICardSelector`, and `IDeckBuilder` are all Scoped (per Blazor Server circuit).
  `ClassificationCache` is the only Singleton in the Agent layer. Never register a Scoped LLM
  service as Singleton — it would capture the per-circuit key.
- **BYOK — SDK seam:** `ClaudeClientFactory` is the only place that calls `new AnthropicClient(...)`.
  `LlmClassifier` and `LlmSelector` receive it via `IClaudeClientFactory`. Keep it that way.
- **BYOK — 401 handling:** `AnthropicUnauthorizedException` is caught in the LLM callers and
  rethrown as `ApiKeyRejectedException`. The UI catches this, calls `Keys.Clear()`, and shows a
  reconnect prompt. Don't swallow it or convert it to a generic build failure.
- **Temperature warning:** `MessageCreateParams.Temperature` is deprecated (`CS0618`) for models
  after Opus 4.6. The current values (0.1, 0.6) work but will need migration if the SDK removes
  backward compatibility.
- **Prompt caching — awaiting SDK API exposure:** Anthropic SDK v12.35.1 is now current (upgraded
  from v12.30.0). Prompt caching via `SystemBlockParam` is not yet exposed in the public C# SDK API,
  despite being supported by the API. Monitor Anthropic SDK releases — when `SystemBlockParam` is
  added to the public API (likely in a future v12.x release), apply this fix to both
  `LlmClassifier.cs` and `LlmSelector.cs`:
  ```csharp
  var systemBlock = new SystemBlockParam
  {
      Text = ClassificationPrompt.SystemPrompt,
      CacheControl = new CacheControlEphemeral(),
  };
  var request = new MessageCreateParams
  {
      System = new MessageCreateParamsSystem([systemBlock]),
      // ... rest of params
  };
  ```
  **Why:** System prompt caching reduces input token cost on repeated calls (e.g., multi-build sessions).
  **Impact:** ~25–30% cost reduction per cached call.
  **Verification:** After implementing, check usage report — `CacheCreationInputTokens` and
  `CacheReadInputTokens` should be > 0 on multi-build runs.
- **SDK versioning:** Anthropic package is now v12.35.1 in `EdhDeckBuilder.Agent.csproj`.
  Review release notes before updating; notify the team of any API changes that affect `LlmClassifier.cs`
  or `LlmSelector.cs`. Check for `SystemBlockParam` public API availability.

## Conventions

- Modern C#: records for data, file-scoped namespaces, nullable enabled, immutability by default.
- **Razor components use code-behind files.** All `@code` blocks in `.razor` files must be extracted
  to corresponding `.razor.cs` files using the `public partial class` pattern. No code blocks directly
  in Razor markup.
- Implement interfaces from `Core/Abstractions/` in the outer layers; don't add new abstractions
  to Core without a good reason.
- Keep secrets (the Anthropic API key) out of source — use user-secrets or environment variables.
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
