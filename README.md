# EdhDeckBuilder

An LLM-assisted Commander (EDH) deck builder in C#/.NET, with a Blazor front end.

## Solution layout

```
EdhDeckBuilder.slnx
├── EdhDeckBuilder.Core            # domain model + rules + templates (no external deps)  ← built
├── EdhDeckBuilder.Infrastructure  # Scryfall + EDHREC clients, local card store         ← next
├── EdhDeckBuilder.Agent           # Anthropic SDK, tool definitions, card classifier    ← next
└── EdhDeckBuilder.Web             # Blazor Web App (the visual, grouped deck view)       ← later
```

Dependency direction points inward: everything references `Core`, `Core` references nothing.
That keeps the domain and the EDH rules pure, fast, and unit-testable without touching the
network or the LLM.

## What's in Core so far

All pure domain code, no external dependencies.

- **`Cards/Color.cs`** — colors as a `[Flags]` enum, so a color identity is a bit set and the
  central legality check (`identity.IsWithin(commander)`) is one bitwise operation.
- **`Cards/Card.cs`** — immutable `Card` record. Color identity and Commander legality are stored
  as given by Scryfall, never recomputed from mana cost or text.
- **`Cards/CardRole.cs`** — the functional buckets (Land, Ramp, CardDraw, Removal, …) the UI
  groups by, plus `ClassificationSource` (where a role assignment came from).
- **`Cards/RoleProfile.cs`** — the multi-role overlap model: a `Primary` role plus `Secondary`
  contributions, each tagged with a `RoleRelation` (`Both` / `EitherOr` / `Switch`) and a coverage
  weight. This is what lets a single card count toward more than one role.
- **`Decks/Deck.cs`** — `Deck` + `DeckSlot`. Supports one or two commanders; color identity is
  their union. `GroupByRole()` (by primary role) feeds the visual layout; `CoverageByRole()` is the
  overlap-aware accounting that can legitimately sum past 99.
- **`Decks/DeckTemplate.cs`** — soft per-role targets (the "stable deck" ratios) with a `Balanced` preset.
- **`Decks/Archetype.cs`** — archetypes as composable *deltas* over the baseline (`ArchetypeProfile`),
  with a starter `ArchetypeLibrary` (Big Mana, Control, Aggro, …).
- **`Decks/TemplateResolver.cs`** — blends weighted archetypes over a baseline and normalizes the
  result to a legal deck size. The deterministic half of the build; the LLM only chooses the weights.
- **`Rules/Rules.cs`** — composable `IDeckRule`s and a `DeckValidator` enforcing the hard format
  rules (100 cards, singleton, color identity, legal commander, banlist).
- **`Abstractions/Abstractions.cs`** — the seams (`ICardRepository`, `ISuggestionSource`,
  `ICardClassifier`) that the Infrastructure and Agent layers will implement.

See `TODO.md` for deferred work (the scoring/fill layer, the classifier, and open design questions).

## Getting it running

```bash
dotnet new sln -n EdhDeckBuilder

dotnet new classlib -o EdhDeckBuilder.Infrastructure
dotnet new classlib -o EdhDeckBuilder.Agent
dotnet new blazor   -o EdhDeckBuilder.Web      # Blazor Web App
dotnet sln add **/**.csproj

dotnet add EdhDeckBuilder.Infrastructure reference EdhDeckBuilder.Core
dotnet add EdhDeckBuilder.Agent          reference EdhDeckBuilder.Core
dotnet add EdhDeckBuilder.Web            reference EdhDeckBuilder.Core EdhDeckBuilder.Infrastructure EdhDeckBuilder.Agent

# The official Anthropic C# SDK is published as the `Anthropic` package — pin the version.
dotnet add EdhDeckBuilder.Agent package Anthropic --version 12.30.0
```

Requires the .NET 10 SDK.
