# CLAUDE.md

Project context and working instructions for Claude Code. Read this, `README.md`, and
`AGENT_PRINCIPLES.md` before doing anything in the Agent layer.

**Document maintenance:** when `README.md` is updated (new capabilities, changed limitations,
new pipeline stages), also review `HOW_IT_WORKS.md` and keep it in sync. `HOW_IT_WORKS.md`
is the user-facing counterpart to `README.md` — it describes the same system from the
perspective of someone using the deck builder rather than building it.

## What this is

An LLM-assisted Magic: the Gathering Commander (EDH) deck builder in C#/.NET 10, with a Blazor
front end. The user drives it in natural language; an agent (Anthropic C# SDK) builds and validates
decks using a pure domain core. `README.md` documents the architecture; `TODO.md` tracks deferred
work; `AGENT_PRINCIPLES.md` is the design rationale document for the Agent layer.

## Current state

All four projects exist and compile. Run `dotnet test Tests` — 225 tests, all green.

| Project | Status |
|---|---|
| `EdhDeckBuilder.Core` | Done — domain model, rules, templates, archetypes, themes, brackets |
| `EdhDeckBuilder.Infrastructure` | Done — Scryfall bulk client, EDHREC client, `SuggestionSource` |
| `EdhDeckBuilder.Agent` | Done — fill engine, LLM seam, pipeline, DI registration |
| `EdhDeckBuilder.Web` | Scaffolded only — boilerplate Blazor Web App, no real UI yet |

The next meaningful work is the Web UI — see `TODO.md`.

## Agent layer — how it works

`Pipeline/DeckBuilder.cs` is the entry point (`IDeckBuilder.BuildAsync`). It runs a 10-stage
staged pipeline; the LLM is consulted at exactly two fixed points:

1. **Classification** — `LlmClassifier` (`claude-haiku-4-5-20251001`, temperature 0.1, batched,
   forced tool call). Assigns `CardRole` + secondary overlaps. Results cached globally by
   `OracleId` except `Plan` and `Synergy`, which are re-classified per build.
2. **Selection** — `LlmSelector` (`claude-sonnet-4-6`, temperature 0.6, per-role call, forced
   tool call). Returns a ranked list with per-card rationale. The fill engine decides count;
   the model never outputs counts.

Everything else — fill order, reconciliation, color-fixing, repair, basic distribution — is
deterministic code in `Fill/` and `Pipeline/`.

All open design decisions from `AGENT_PRINCIPLES.md` are now closed. Do not reopen them
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
- **Classification cache:** `ClassificationCache` is a singleton; Plan and Synergy are never
  served from it. Don't cache them.
- **Temperature warning:** `MessageCreateParams.Temperature` is deprecated (`CS0618`) for models
  after Opus 4.6. The current values (0.1, 0.6) work but will need migration if the SDK removes
  backward compatibility.

## Conventions

- Modern C#: records for data, file-scoped namespaces, nullable enabled, immutability by default.
- Implement interfaces from `Core/Abstractions/` in the outer layers; don't add new abstractions
  to Core without a good reason.
- Keep secrets (the Anthropic API key) out of source — use user-secrets or environment variables.
- Tests live in `Tests/`. No mocking libraries — manual mocks only (the project has no Moq/NSubstitute
  reference). Keep tests fast: LLM calls must be behind the `ILlmClassifier`/`ICardSelector`
  interfaces and replaced with mocks in tests.
