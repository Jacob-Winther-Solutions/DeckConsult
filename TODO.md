# TODO — EdhDeckBuilder

A living list of deferred work. Check items off as they land.

---

## Web UI  (core done — two items deferred)

- [x] Wire `IDeckBuilder` into a Blazor page / component tree.
- [x] Role-grouped deck view: expandable role buckets, each card showing `CardSuggestion.Reason`;
      secondary-role contributions shown in an "Also contributes" strip per bucket.
- [x] Basic land section: display `DeckBuildResult.BasicLandCounts`.
- [x] Runner-up panel: show `DeckBuildResult.RunnerUps` (collapsed by default).
- [x] Coverage summary: table comparing `DeckBuildResult.ActualCoverage` against
      `DeckBuildResult.PlannedTemplate` targets with a progress bar per role.
- [x] Cut suggestions: surfaced inline in role buckets (warning badge + "consider cutting").
- [x] Commander input: debounced search box with Scryfall autocomplete, supports partner pairs.
- [x] Archetype / theme picker: weight sliders for archetypes; 29 preset themes with weight,
      tune-preset form (absolute slot values), and custom-theme escape hatch.
- [x] Three deck views: By Role (role buckets), By Type (card-type buckets, priority-ordered),
      All Cards (alphabetical table with role badges).
- [x] Budget input in the UI (see Budget section below).
- [ ] Deck persistence (save/load to local storage or a backend store).
- [ ] Refactor UI to make components resusable for future pages, e.g. "Commander Discovery" and "Historic Brawl" pages.

---

## Budget-aware card selection  (done)

Players on tight budgets should get a competitive deck within their price range rather than
a list full of expensive staples they have to manually replace. Two independent budget axes:

- **Per-card budget** (`MaxCardPriceUsd`) — no single card may exceed this amount. The
  primary lever: directly prevents the LLM from selecting expensive staples.
- **Total deck budget** (`TotalBudgetUsd`) — the sum of all 99 cards must stay within this
  amount. Useful when a player is fine with one or two expensive pieces but wants to stay
  under a total spend. Both fields are nullable/optional and can be combined.

- [x] **Add both budget fields to `SoftConstraints`** — `decimal? MaxCardPriceUsd` and
      `decimal? TotalBudgetUsd`. The builder already passes `SoftConstraints` to the selector
      prompt, so this is the natural place to carry them.
- [x] **Fetch card prices.** Scryfall bulk card data includes `prices.usd` and `prices.usd_foil`.
      Store the non-foil price on `Card` at ingestion time. Scryfall bulk data is already cached
      locally, so no extra network call is needed.
- [x] **Pass budget to the selector prompt.** The selection prompt should instruct the LLM to
      deprioritize cards that would breach either threshold and prefer affordable alternatives.
      Budget is a soft preference — if no affordable card can fill a role, pick the best
      available and flag it in the result.
- [x] **Surface budget violations in the result.** Add a `BudgetWarnings` field (or reuse
      `CoverageWarnings`) listing any cards that exceeded `MaxCardPriceUsd`, plus the total
      deck price so the user can see at a glance whether they are within `TotalBudgetUsd`.
- [x] **UI:** Two budget fields on the deck-build form ("max per card" and "total deck");
      highlight over-budget cards in the deck view; show running total price in the header.
- [x] **Tests:** Unit tests for `FilterPool` (per-card pre-filter drops known over-budget cards,
      keeps null-price cards), `RepairBudgetExcess` (swaps costliest card for cheapest same-role
      alternative, stops when within budget, marks cards as tried when no replacement exists),
      and `BuildBudgetWarnings` (per-card and total violations reported correctly).

---

## Commander selection / discovery  (new feature)

Currently the user must already know which commander they want. A discovery mode would let
them describe a strategy and get back a shortlist of commanders that fit, before building a deck.

- [ ] **Design the input model.** The user provides archetype(s), theme(s), optional budget,
      optional colors or color identity constraints, and optionally a free-text description
      ("I want a grindy aristocrats deck that can play against Bracket 3–4").
- [ ] **Query the commander pool.** Scryfall can return all legendary creatures legal in
      Commander filtered by color identity. This becomes the candidate set.
- [ ] **Score and rank commanders.** Ask the LLM to evaluate each candidate against the
      stated strategy: does this commander's abilities actively support the archetype and theme,
      or is it a generic good-stuff commander? Return a ranked shortlist (top 5–10) with a
      one-paragraph explanation per candidate.
- [ ] **Wire into the pipeline.** Selecting a commander from the shortlist should feed directly
      into the existing `IDeckBuilder.BuildAsync` flow, pre-populating the archetype/theme
      weights the discovery mode resolved.
- [ ] **Consider a new interface `ICommanderSelector`** in the Agent layer, parallel to
      `ILlmClassifier` and `ICardSelector`, so the LLM call is mockable and independently
      testable.
- [ ] **UI:** A "Help me choose a commander" entry point before the deck-build form. Shows
      the shortlist with art, color identity, and the LLM's explanation; user clicks one to
      proceed to the full build.

---

## Additional card sources — Aetherhub + mtgrocks  (prerequisite for Brawl)

These sources are useful for Commander too, not just Brawl. Implement them as general
`ISuggestionSource` / supplementary data providers before building Brawl-specific support.

- **Aetherhub** — has a Commander meta page (popular commanders and their win-rate / play-rate
  data) and Historic Brawl deck lists. A Commander `ISuggestionSource` backed by Aetherhub
  would complement EDHREC with meta-relevance signals.
- **mtgrocks** — has a Commander staples page that tracks which cards appear most frequently
  across top-performing Commander lists. Useful as a signal for cards that are strong regardless
  of commander, supplementing EDHREC's per-commander inclusion rates.

- [ ] Investigate Aetherhub's API / scraping surface for Commander meta data and per-commander
      card lists. Determine whether it returns JSON or requires HTML parsing.
- [ ] Implement `AetherhubCommanderSource : ISuggestionSource` in Infrastructure. Cache
      per-commander (alongside the existing EDHREC cache).
- [ ] Investigate mtgrocks for Commander staples data (likely a static/semi-static list).
      Implement `MtgrocksStaplesSource` — could be a supplementary signal rather than a full
      `ISuggestionSource` (e.g. a weight bump on cards that appear on the staples list).
- [ ] Decide how multiple `ISuggestionSource` implementations are merged in `DeckBuilder`.
      The current merge keeps the highest inclusion per card; ensure the merge strategy still
      makes sense when sources have different inclusion scales.

---

## Historic Brawl format support  (new format — depends on Additional sources above)

Historic Brawl on MTG Arena is 100-card singleton, same physical count as Commander, but
with meaningful differences: 1-vs-1, an **eternal** card pool (no rotation — cards are only
removed via the Historic Brawl ban list when sets release new cards are added), and two
distinct queues with different power expectations.

Key differences from EDH to plan around:

- **Card pool:** Scryfall exposes `legalities.historicbrawl`. Card ingestion needs a separate
  flag rather than reusing the Commander legal pool. The pool is eternal — no rotation, just
  a separate ban list.
- **Two queues — ranked vs. casual:** MTG Arena is adding a ranked queue (heavily meta-
  oriented, expect optimized lists) and a non-ranked queue (more casual). This is analogous
  to the bracket system in Commander and should be part of the input model — the builder
  should know which queue the user is targeting and adjust the `DeckTemplate` baseline and
  selector guidance accordingly.
- **1v1 meta:** Board wipes are weaker (only one opponent), reactive spells and counterspells
  matter more, and go-wide threats are relatively stronger. The `DeckTemplate` targets need a
  Brawl-specific baseline — the Commander baseline will produce badly tuned lists.
- **Sources:** EDHREC does not cover Historic Brawl. Use Aetherhub (Brawl deck lists and
  meta reports) and mtgdecks.net (Brawl commander-specific recommendations). Both require
  their own `ISuggestionSource` implementations in Infrastructure.
- **Commander legality:** Any legendary creature or planeswalker legal in the format can be
  the commander — broader than EDH (planeswalkers allowed). `CanBeCommander` logic needs a
  format-aware variant.
- **Arena-only cards:** Alchemy and Historic Anthology cards exist only on Arena. Scryfall
  includes them; the card model may need an `IsArenaOnly` flag so users can choose to include
  or exclude them.

Suggested approach: model this as a second `FormatProfile` (alongside Commander), not a
separate codebase. Most of the Agent pipeline (fill engine, LLM seam, color-fixing) is
format-agnostic. Only `ISuggestionSource`, the `DeckTemplate` baseline, commander legality
rules, and card ingestion need format-specific variants.

- [ ] Define `FormatProfile` (or equivalent) in Core: legality check, deck size, commander
      count, baseline `DeckTemplate`, and queue/power-level model.
- [ ] Add `historicbrawl` legality flag and `IsArenaOnly` flag to `Card` ingestion.
- [ ] Implement `AetherhubBrawlSource` and `MtgdecksBrawlSource` in Infrastructure.
- [ ] Create Brawl `DeckTemplate` baselines — one for ranked (tighter, more interaction)
      and one for casual — or model it as a Brawl-specific bracket equivalent.
- [ ] Update `IDeckBuilder.BuildAsync` (or a new `IBrawlDeckBuilder`) to accept a
      `FormatProfile` and route to the correct template, sources, and legality rules.
- [ ] Test with a known strong Historic Brawl commander (e.g. Atraxa, Raffine, Sheoldred).

---

## Multi-role classification  (partially deferred)

- [x] Data model: `RoleProfile` (primary + secondary contributions), `RoleRelation`
      (Always / Modal / Transform), coverage accounting on `Deck`.
- [x] Classifier produces `RoleProfile`s — `LlmClassifier` via forced tool call.
- [ ] Context-aware classification: e.g. Jeska's Will behaves differently with vs. without
      the commander on board; depends on commander castability and deck context.
- [ ] Tune default coverage weights (Always=1.0, Modal=0.5, Transform=0.75 are currently
      baked into Core defaults; may need per-commander calibration once real builds are tested).
- [x] Coverage-gap report / template-adherence warnings in the UI — rendered in the By Role
      view as an alert above the role buckets.

---

## Other deferred

- [x] `CanBeCommander` logic at Scryfall ingestion — extended to catch legendary creatures
      AND any card whose oracle text contains "can be your commander" (planeswalkers with the
      RC ruling, special cases). `ScryfallMapper.IsCommanderEligible`. 4 new tests.
- [ ] **Colorless commanders** (e.g. Kozilek) run Wastes as their basic land.
      `DeckBuilder.DistributeBasics` returns an empty dict when `ColorIdentity == Color.None`.
      Wastes handling needs to be added before colorless commanders are supported.
- [ ] **MDFC land credit assignment.** The `LandCredit` field on `FillCandidate` exists and is
      plumbed through; `LlmClassifier` needs to assign non-zero values based on how playable the
      land face is for the given commander (currently defaults to 0 for all cards).
- [x] Power-bracket integration into selector prompt — `SelectionPrompt.AppendBracketGuidance`
      emits bracket number, name, and description; Brackets 1–2 list all Game Changers to avoid;
      Brackets 4–5 encourage Game Changers. 7 new tests.
- [ ] Per-call temperature audit: `Temperature` is deprecated for models after Claude Opus 4.6.
      Once the SDK removes backward-compatibility handling, update both `LlmClassifier` and
      `LlmSelector` to remove the field or replace it with the new mechanism when available.
- [ ] (Stretch) Opening-hand / curve simulation to sanity-check consistency.

---

## Done

- [x] **Core** — domain model, rules, templates, archetypes, themes, bracket system.
- [x] **Infrastructure** — Scryfall bulk client + EDHREC client + `SuggestionSource`.
- [x] **Agent — models & interfaces** — `BuildContext`, `BuildState`, `FillCandidate`,
      `SoftConstraints`, `CardSuggestion`, `DeckBuildResult`, `ILlmClassifier`, `ICardSelector`,
      `IDeckBuilder`, `ClassificationResult`, `SelectionResult`.
- [x] **Agent — fill engine** — `FillEngine` (greedy fill + bounded monotonic reconciliation
      swap loop, max 50 iterations), `ColorFixingPass` (Pass C: pip-demand scoring, 8-basic
      floor, 50% non-basic cap), 31 unit tests.
- [x] **Agent — LLM seam** — `LlmClassifier` (Haiku, forced tool call, batched, globally
      cached except Plan/Synergy/Payoff), `LlmSelector` (Sonnet, forced tool call, per-build rationale
      capture), `ClassificationCache`, `ClassificationPrompt`, `SelectionPrompt`.
- [x] **Agent — pipeline** — `RepairEngine` (deterministic CI-violation repair + result
      assembly), `DeckBuilder` (10-stage orchestration), 5 integration tests.
- [x] **Agent — DI** — `ServiceCollectionExtensions.AddAgent(configuration)`.
