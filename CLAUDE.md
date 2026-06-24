# CLAUDE.md

Project context and working instructions for Claude Code. Read this and `README.md` before doing anything.

## What this is

An LLM-assisted Magic: the Gathering Commander (EDH) deck builder in C#/.NET 10, with a Blazor
front end. The user drives it in natural language; an agent (Anthropic C# SDK) builds and validates
decks using a pure domain core. `README.md` documents the architecture; `TODO.md` tracks deferred work.

## Current state

`EdhDeckBuilder.Core/` already exists and is the finished, reviewed domain layer. It is the
source of truth for the design. The other three projects do **not** exist yet and need scaffolding.

## Your task (initial setup)

1. Create the solution and add the existing Core project:
   - `dotnet new sln -n EdhDeckBuilder`
   - `dotnet sln add EdhDeckBuilder.Core/EdhDeckBuilder.Core.csproj`
2. Scaffold the three remaining projects at the repo root:
   - `EdhDeckBuilder.Infrastructure` — class library (Scryfall + EDHREC clients, local card store)
   - `EdhDeckBuilder.Agent` — class library (Anthropic SDK, tool definitions, card classifier)
   - `EdhDeckBuilder.Web` — Blazor Web App (the visual, grouped deck view)
3. Wire project references inward:
   - Infrastructure → Core
   - Agent → Core
   - Web → Core, Infrastructure, Agent
4. Add the official Anthropic SDK to the Agent project: `dotnet add ... package Anthropic`.
   It is currently in **beta** — pin an explicit version, don't float it.
5. Set every new project to `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`
   to match Core.
6. Run `dotnet build` and confirm the solution compiles before stopping. Core has not been built in
   this environment yet, so report any errors you find rather than assuming it's clean.

Do not start implementing Infrastructure, Agent, or Web logic in this pass — scaffold, wire, build, stop.

## Guardrails — do not redesign Core

These decisions are intentional. Keep them unless the user explicitly asks to change them.

- **Dependency direction points inward.** Core references nothing external. Infrastructure and
  Agent depend on Core, never the reverse. The LLM and network live outside Core.
- **Scryfall owns the facts.** `Card.ColorIdentity` and `Card.CommanderLegality` are stored as
  Scryfall reports them. Never derive color identity from mana cost or oracle text.
- **Color identity is a `[Flags]` enum**; the subset check is the one in `ColorExtensions.IsWithin`.
- **Two kinds of counting are distinct and both matter:** physical count (each card's `PrimaryRole`,
  must total 100 with commanders) vs. role *coverage* (`Deck.CoverageByRole()`, counts overlap, may
  exceed 99). Don't collapse them.
- **Archetypes are deltas, resolved deterministically.** The LLM only emits weighted archetypes;
  `TemplateResolver` turns them into counts. Don't have the model output raw card counts.
- **Hard rules vs. soft templates are separate.** `Rules/` enforces legality; `DeckTemplate`/
  archetypes only guide construction.

## Conventions

- Modern C#: records for data, file-scoped namespaces, nullable enabled, immutability by default.
- Implement the interfaces in `Core/Abstractions/` from the outer layers; don't add new abstractions
  to Core without reason.
- Keep secrets (the Anthropic API key) out of source — use user-secrets or environment variables.

## Deferred — see TODO.md, don't build yet

The scoring/fill layer and the card classifier are intentionally not built. Don't implement them as
part of setup. The "target semantics" question (whether template targets are physical or coverage)
is open and logged — don't silently resolve it.
