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
- [x] Deck download: "Export Build Report" button generates a `.md` file and streams it as a browser download
      (see Deck Download section below).
- [x] Refactor UI to make components reusable for future pages, e.g. "Commander Discovery" and "Historic Brawl" pages.

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

## Commander selection / discovery  (done — one enhancement deferred)

Users can now describe a strategy and get a ranked LLM shortlist of commanders that fit via a
dedicated `/discover` page. The page is standalone and independent of the CommanderBuilder.
Two discovery modes: **Guided** (archetype/theme-driven) and **Custom** (free-text description).

- [x] **Design the input model.** The user provides archetype(s), theme(s), optional budget,
      optional colors or color identity constraints, and optionally a free-text description
      ("I want a grindy aristocrats deck that can play against Bracket 3–4").
- [x] **Query the commander pool.** Scryfall returns all legendary creatures legal in
      Commander filtered by color identity. This becomes the candidate set.
- [x] **Score and rank commanders.** LLM evaluates each candidate against the stated strategy:
      does this commander's abilities actively support the archetype and theme, or is it
      generic good-stuff? Returns a ranked shortlist (top 5–10) with one-paragraph explanation
      per candidate via `ICommanderSelector` and `LlmCommanderSelector`.
- [x] **Core infrastructure** — `ICardRepository.GetCommandersAsync` with color-identity filtering
      and exact-match option in `CardRepository`.
- [x] **Agent layer** — `CommanderDiscoveryRequest`, `CommanderDiscoveryResult`, `CommanderSuggestion`,
      `ICommanderDiscovery`, `CommanderDiscovery` service with batching (≤150 single call,
      >150 two-pass). `ICommanderSelector` interface + `LlmCommanderSelector` (user-selected model,
      forced tool call, whitelist filtering). `CommanderSelectionPrompt` static system prompt +
      tool schema.
- [x] **Web UI** — Standalone `/discover` page. `DiscoveryTab.razor/.cs` with form (color picker,
      archetype/theme/bracket/budget selectors, free-text description), LLM call with progress,
      results grid. `CommanderSuggestionCard.razor` displays art, rationale, rank.
      `ColorIdentityPicker.razor` (any-color toggle + 5-color checkboxes + exact-match checkbox).
- [x] **Tests** — `CommanderDiscoveryTests` (pool size, batching, color filters),
      `CommanderSelectionPromptTests` (message formatting), `MockCommanderSelector` manual mock.
      All 281 existing tests pass; Commander Discovery tests included.
- [x] **Custom tab for CommanderDiscovery:** The discovery page now has two tabs: Guided (archetype/theme-driven,
      LLM-assisted) and Custom (free-text description). Custom tab mirrors the CustomTab pattern from
      CommanderBuilder but accepts a deck description instead of a commander. Both tabs surface results
      as ranked `CommanderSuggestionCard`s that link to the builder with pre-selected commanders.
- [x] **UI polish:** Color Identity picker now shows the "Exactly this identity" checkbox inline
      with the color buttons (flexbox layout) for better visibility. Token usage logging is consistent
      across all discovery and build flows (summary header, table, total cost).
- [x] **Partner and partner-with support (Discovery & Core):** Discovery now surfaces partner pairs via dedicated
      Core `PartnerCombo` entities and deterministic Infrastructure index. Supports all variants:
      Partner, Partner with, Background, Friends Forever, Doctor's Companion, Survivors, Character Select, Father & Son.
      - [x] `IEdhrecClient.GetPartnersPageAsync()` — fetches EDHREC partner index at startup
      - [x] `PartnershipIndexBuilder` — deterministic pairing extraction by type (sequential pairs, all-vs-all, Doctor first + companions, etc.)
      - [x] `CardRepository` — accepts EDHREC partnerships at construction; partnership eligibility enforced by Core rules
      - [x] `CommanderDiscovery` — surfaces all legal partner combinations in results (top 10 or all if ≤10)
      - [x] DI registration — `IEdhrecClient` seam, `AddHttpClient<EdhrecClient>` ordering
      - [x] Test coverage — partnership extraction, validity checks, discovery result limits (9 new tests for variants)
- [ ] **Partner support in Deck Builder (EDHREC):** The Commander Deck Builder (`DeckBuilder.cs`) accepts
      multiple commanders and correctly combines color identity + merges recommendations. **However**,
      it currently queries `ISuggestionSource` per individual commander and merges results. EDHREC
      **does support partner pairings** — each partner pair has its own recommendation page:
      `https://edhrec.com/commanders/{card1-slug}-{card2-slug}` (e.g., Thrasios+Tymna:
      `https://edhrec.com/commanders/thrasios-triton-hero-tymna-the-weaver`).

      **Implementation tasks:**
      - [ ] Extend `EdhrecClient` with `GetRecommendationsForPartnerPairAsync(Card first, Card second)`
      - [ ] Construct partner pair URL slug: `{first.Name.ToSlug()}-{second.Name.ToSlug()}` (match EDHREC's format)
      - [ ] At build time in `DeckBuilder`, detect partner pairs via `ICardRepository.GetPartnerCombosAsync()`
      - [ ] Call partner endpoint instead of merging two singleton pools
      - [ ] Fallback to merged-singles approach if partner pair endpoint returns empty or 404
      - [ ] Update `GatherPoolAsync()` to handle partner detection and routing

      **Scope:** ~2–3 hours (EDHREC client method + URL slug logic + partner detection in fill pipeline).
      **Status:** Deferred (Phase 5). Core partnership support is complete; this is a build-time optimization to avoid card-list merging.

---

## Additional card sources — Commander Spellbook + TopDeck.gg  (prerequisite for Brawl / Duel Commander)

See `TODO/DATA_SOURCES.md` for the full spec, verified API details, and the excluded-sources
list. Aetherhub and mtgrocks are explicitly excluded (no sanctioned API / bot-blocked). The
sanctioned sources to add are:

- **Commander Spellbook** (MIT-licensed open API) — combo detection and bracket estimation.
  Feeds the Synergy/Plan roles and the bracket signal. Applies to Commander, Brawl, and
  Duel Commander alike. Endpoints: `find-my-combos` (POST card list → combos present +
  one-card-away) and `estimate-bracket`.
- **TopDeck.gg** (free API key, attribution required) — competitive card-frequency signal
  for Commander and Duel Commander (Brawl is not covered). A **periodic ingest job**, not a
  live per-build call: pull tournaments, aggregate card frequency by commander, store locally.
  Visible credit + link to TopDeck.gg must appear in the UI wherever this data is surfaced.
- **EDHREC — extend to Brawl paths** — the existing client already handles Commander; extend
  it to resolve the Brawl URL variants (`json.edhrec.com`) for Historic Brawl support. Note:
  EDHREC does not model Duel Commander's banlist/metagame — do not use it as a DC source.
- **MTGJSON** — optional; only adopt if offline bulk card data or preconstructed deck seed
  lists become a goal. Defer until there is a concrete use case.

**Owner decisions required before implementation:**

- [ ] **TopDeck.gg aggregation weighting:** raw card frequency vs. standing-weighted (cards
      in top-finishing decks count more). Determines whether the local aggregate table stores
      standings alongside counts.
- [ ] **Adopt MTGJSON now or defer.** Only worth adding for offline/precon support.
- [ ] **Historic Brawl competitive popularity gap.** No sanctioned open source exists
      (Untapped.gg is commercial with no public API). Decide whether to accept EDHREC +
      Scryfall alone for Brawl, or leave popularity as a deferred TODO.

**Implementation tasks:**

- [ ] Implement `CommanderSpellbookClient` in Infrastructure: `find-my-combos` + `estimate-bracket`
      endpoints, cached per card-list hash. Define `IComboSource` in Core Abstractions.
- [ ] Implement `TopDeckIngestJob` in Infrastructure: periodic pull of EDH/DC tournaments,
      aggregate `deckObj` card frequency by commander into a local store. Define
      `ICompetitiveMetaSource` in Core Abstractions.
- [ ] Extend `EdhrecClient` to resolve Brawl URL path variants alongside the existing
      Commander paths.
- [ ] Decide how multiple `ISuggestionSource` implementations are merged in `DeckBuilder`.
      The current merge keeps the highest inclusion per card; revisit this once sources with
      different inclusion scales (EDHREC % vs. TopDeck.gg raw frequency) are combined.
- [ ] Add TopDeck.gg attribution credit to the Web UI (visible link) wherever competitive
      frequency data is surfaced.

---

## Multi-format support  (new formats — depends on Additional sources above)

### Historic Brawl on MTG Arena

Historic Brawl is 100-card singleton, same physical count as Commander, but
with meaningful differences: 1-vs-1, an **eternal** card pool (no rotation — cards are only
removed via the Historic Brawl ban list when sets release), and two
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

### Duel Commander (French Commander)

Duel Commander is 1v1 100-card singleton with a separate, more aggressive banlist than EDH.
The format is primarily European but has a global competitive community with top-8 finishes
published regularly.

- [ ] Research Duel Commander legality — card pool differs from EDH (separate banlist).
  Scryfall does not expose a dedicated legality flag; use an external source (e.g. Archidekt,
  Scryfall's `restricted` flag, or French Commander's official banlist).
- [ ] Integrate competitive meta source — TopDeck.gg covers Duel Commander tournaments.
- [ ] Create Duel Commander `DeckTemplate` baseline — tuned for 1v1 faster pace vs. multiplayer EDH.
- [ ] Update `FormatProfile` to support Duel Commander legality and bracket/queue model.

### Pauper EDH (Pauper Commander)

Pauper EDH is a casual variant where all cards in the deck (including the commander) must
have been printed at common rarity at some point. No official format body, but active community.

- [ ] Integrate Scryfall's rarity data — cards must be `rarity == "common"`.
- [ ] Confirm banlist source (if any) — Pauper EDH is community-managed.
- [ ] Create Pauper EDH `DeckTemplate` baseline — tuned for constrained power level.
- [ ] Update `FormatProfile` to support Pauper EDH legality.

### Peasant Commander

Peasant Commander allows up to 5 uncommon cards in the deck (commander can be uncommon or rare,
but only 5 uncommons allowed in the 99). Similar community-managed status to Pauper EDH.

- [ ] Research Peasant Commander rules and banlist source.
- [ ] Implement rarity-counting logic — track uncommons per deck, flag when exceeding 5.
- [ ] Create Peasant Commander `DeckTemplate` baseline.
- [ ] Update `FormatProfile` to support Peasant Commander legality and rarity constraints.

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
- [x] **Colorless commanders** (e.g. Kozilek) run Wastes as their basic land.
      `DeckBuilder.DistributeBasics` returns an empty dict when `ColorIdentity == Color.None`.
      Wastes handling needs to be added before colorless commanders are supported.
- [x] **MDFC / DFC back-face data and land credit assignment.** `Card.BackFaceTypeLine` and
      `Card.BackFaceText` are now populated at ingestion for **all** double-faced cards (MDFCs,
      transform, creature//planeswalker, etc.), not just land-backed ones. The classifier prompt
      includes both lines so the LLM can evaluate any DFC by both faces. Land credit is only
      non-zero when the back face is a Land type — enforced deterministically by
      `ClassificationSanitizer.SanitizeLandCredit`, which `LlmClassifier` chains after the
      existing role sanitizer using the full `Card` object (not just `CardType`). 12 new tests.
- [x] Power-bracket integration into selector prompt — `SelectionPrompt.AppendBracketGuidance`
      emits bracket number, name, and description; Brackets 1–2 list all Game Changers to avoid;
      Brackets 4–5 encourage Game Changers. 7 new tests.
- [ ] Per-call temperature audit: `Temperature` is deprecated for models after Claude Opus 4.6.
      Once the SDK removes backward-compatibility handling, update both `LlmClassifier` and
      `LlmSelector` to remove the field or replace it with the new mechanism when available.
- [ ] **Wire saved-result limit to subscription tier.** Currently hardcoded as
      `DeckResultStorage.DefaultMaxSavedResults = 3` in `EdhDeckBuilder.Web/Services/DeckResultStorage.cs`.
      The JavaScript function `saveDeckResult(key, value, maxResults)` already accepts the limit
      as a parameter — no JS changes needed. On the C# side, resolve the limit from a subscription
      or feature-flag service and pass it to `JS.InvokeVoidAsync("saveDeckResult", key, json,
      resolvedLimit)` in `GuidedTab.razor.cs` and `CustomTab.razor.cs`. The two call sites are the
      only places that need updating.
- [ ] (Stretch) Opening-hand / curve simulation to sanity-check consistency.

---

## Deck Download — Markdown export (done)

No server-side deck storage. Instead, a finished deck can be exported as a self-contained
`.md` file the user saves locally. The file is generated server-side from `DeckBuildResult`
and streamed as a browser download — pure Web layer concern, no Core changes.

**File contents (in order):**
- Header: commander(s), archetype/theme weights, bracket, budget constraints, build date.
- Role buckets: one section per `CardRole`, each card on its own line with the
  `CardSuggestion.Reason` inline — same structure as the By Role view.
- Runner-ups: collapsed appendix listing `DeckBuildResult.RunnerUps` by role.
- Coverage summary: planned vs. actual counts per role.
- Raw decklist: plain `1 Card Name` lines (basic lands with their counts), ready to paste
  into Moxfield, Archidekt, or any other deck builder that accepts text import.

**Implementation tasks:**
- [x] `DeckMarkdownExporter` service in the Web project: accepts `DeckBuildResult` +
      `BuildContext`, returns a `string` (the markdown). No infrastructure dependencies.
- [x] Blazor "Export Build Report" button: calls the exporter, then uses JS interop
      (`URL.createObjectURL`) to trigger a `.md` file download.
      Filename: `<commander-name>-build-report.md` (slugified).
- [x] Tests: snapshot-style unit test — given a known `DeckBuildResult`, assert the
      markdown output contains the expected sections and raw decklist lines.

---

## BYOK — Per-user Anthropic API keys

Users supply their own Anthropic API key (created at console.anthropic.com). Usage bills
to their account at pay-per-token rates. This is the only sanctioned model for a public
app — Anthropic's terms prohibit routing all users through a single company key.

See `TODO/BYOK_API_KEY.md` for the full design spec.

**Key decisions (already made):**
- Blazor Server hosting: the key lives server-side in a scoped service (one per circuit),
  never sent to the browser.
- Persistence: encrypted HttpOnly cookie via ASP.NET Core Data Protection. The user pastes
  once, checks "remember my key", and never sees the prompt again until the key expires or
  they explicitly disconnect.
- Data Protection keyring persisted via a named Docker volume (see Deployment section).

**Implementation tasks:**
- [x] `IClaudeApiKeyProvider` + `SessionApiKeyProvider` (scoped) in the Agent project.
      `SessionApiKeyProvider` holds the in-memory key for the circuit; exposes `Set` / `Clear`.
- [x] `IClaudeClientFactory` + `ClaudeClientFactory` (scoped): builds an `AnthropicClient`
      per call from the provider. This is the sole seam touching the SDK constructor. Updated
      `LlmClassifier` and `LlmSelector` to accept the factory instead of a singleton client.
      Removed the singleton `ANTHROPIC_API_KEY` client construction from DI.
- [x] `IClaudeKeyTester` + `ClaudeKeyTester`: fires a minimal 1-token Haiku call to validate
      a key before accepting it. Used by the settings UI.
- [x] **Cookie persistence**: encrypted value written via JS interop using ASP.NET Core Data
      Protection. Read back in `OnAfterRenderAsync` after the circuit connects. "Remember my
      key" checkbox defaults on. Cookie is not HttpOnly (written from JS) but payload is opaque.
- [x] `ApiKeySettings.razor` component: password input + "Connect" / "Test key" / "Disconnect"
      buttons, "Remember my key" checkbox (default on). Shows connected/disconnected state.
      Gates the build form: shows info alert instead of form sections when no key is connected.
- [x] Handle HTTP 401 from Anthropic at the agent boundary: `AnthropicUnauthorizedException`
      caught in `LlmClassifier`/`LlmSelector`, rethrown as `ApiKeyRejectedException`.
      `CommanderBuilder` catches it, calls `Keys.Clear()`, shows reconnect message.
- [x] DI registration: `SessionApiKeyProvider` as Scoped (twice — concrete + interface).
      All LLM-dependent services (`ILlmClassifier`, `ICardSelector`, `IDeckBuilder`) changed
      from Singleton to Scoped. `ClassificationCache` stays Singleton.
- [x] **Model picker**: `SelectedModel` on `SessionApiKeyProvider`; exposed via
      `IClaudeClientFactory.SelectionModel`; `LlmSelector` uses it at call time.
      Classification always uses Haiku. Dropdown: Haiku 4.5 / Sonnet 5 / Opus 4.8.
- [ ] (Stretch) Approximate token/cost estimate displayed after each build, since users now
      pay directly and will want visibility.

---

## TCGPlayer affiliate linking

A "Buy this deck on TCGplayer" action on a finished deck. Sends the full decklist into
TCGplayer's Mass Entry cart tool, tagged with an affiliate code so referred purchases earn
commission (~3.5%, first-click 48-hour window). No TCGplayer API account required — this
is a URL/form builder only.

See `TODO/TCGPLAYER_AFFILIATE_LINKING.md` for the full design spec.

**Prerequisites (non-code actions — must happen before commission tracking works):**
- [ ] **WotC Fan Content Policy check**: confirm the policy permits monetization for a tool
      like this before enabling any affiliate links. Legal/product decision — do not assume.
- [ ] **Apply to TCGplayer's affiliate program** via Impact (impact.com). The affiliate code
      comes from the Impact dashboard after acceptance. Commission: ~3.5% per sale.

**Implementation tasks:**
- [ ] `TcgPlayerLinkOptions` (singleton from config): holds `AffiliateCode` and `Medium`.
      Pull from configuration/secrets — never hardcode.
- [ ] `TcgPlayerMassEntryLinkBuilder` (scoped): `BuildGetUrl` for single-card/preview links;
      `BuildPostForm` (returns action URL + raw card list value) for full decks. POST is
      preferred for 100-card Commander lists to avoid URL length limits.
- [ ] Map `DeckBuildResult` → `IReadOnlyList<CartLine>` at the call site in the Web layer:
      commanders at qty 1, all nonland spells at qty 1 (singleton), basics at their real counts.
      Keep Core pure — no TCGplayer types in the domain.
- [ ] `BuyDeckButton.razor`: renders a native `<form method="post">` with hidden `productline`
      and `c` inputs. `data-enhance="false"` to prevent Blazor enhanced-nav from intercepting.
      `target="_blank"` to open the cart in a new tab. Do not use `fetch` — CORS blocks
      cross-origin POST and prevents following the 303 redirect to the cart.
- [ ] DI registration: `TcgPlayerLinkOptions` as singleton; `TcgPlayerMassEntryLinkBuilder`
      as scoped.
- [ ] (Future) Plain text decklist export for users who prefer to paste into Mass Entry
      themselves — shares the `CartLine` mapping with the buy button.
- [ ] (Future) Multi-retailer support (Card Kingdom etc.) — keep the builder interface shaped
      so a second provider can slot in without redesign.

---

## Deck Analysis & Enhancement Track  (new — three features)

See `TODO/TODO_new_features.md` for the full brainstorm session with design context and open questions.

### 1. Deck Analyzer

Given an existing decklist (not built by this tool), classify it against the role taxonomy,
estimate bracket/power level, and generate staged budget upgrade paths.

**Features:**
- [ ] **Decklist ingestion**: accept pasted decklists in common export formats (at minimum:
      plain `1 Card Name` per line, Arena format). Resolve each line to Scryfall via existing
      client. Handle fuzzy matches, DFCs, misprints, not-found cards (report to user).
- [ ] **Commander detection**: separate commander(s) from the 99 — may need explicit user input
      if format doesn't mark it.
- [ ] **Role classification**: reuse `LlmClassifier` on pasted deck to tag cards with roles,
      surface `CoverageByRole`, identify significant gaps vs. baseline template targets.
      Output a report structure (reusable by subsequent features).
- [ ] **Bracket estimation**: reuse/refactor existing bracket logic (currently generative only)
      to evaluate a deck and estimate its bracket. Include human-readable explanation (e.g.
      "N fast mana + M tutors").
- [ ] **Budget upgrade paths**: given classified/gapped decklist, generate staged upgrade
      suggestions at multiple budget tiers, mapped to identified role gaps. Reuse selection
      logic rather than building parallel system.
- [ ] **User experience feedback**: optional free-text field where user describes what they found
      working and not working with the deck (e.g. "I always find that I cannot recover from a 
      board wipe" or "It builds well, but I cannot finish the game"). This informs gap analysis 
      and upgrade suggestions — if user reports recovery issues, prioritize board wipe protection 
      in upgrades; if they report finishing issues, prioritize payoff/draw/tutors.

**Open questions for Master:**
- Exact budget tier breakpoints.
- Which export formats to prioritize (Moxfield, Archidekt, Arena, MTGO).
- Whether bracket-estimation needs new logic or can reuse generative constraints.
- How to weight user experience feedback in upgrade suggestions (use as primary signal, secondary, or informational only)?

### 2. Combo Finder

Given a decklist, surface Commander Spellbook combos that are "close" — achievable with a
small number of additional cards.

- [ ] Integrate Commander Spellbook REST API client in Infrastructure (verify not already
      scoped in `DATA_SOURCES.md`).
- [ ] Query for combos where most pieces are already present; define and implement "distance"
      metric (e.g. combos missing 1–2 cards, ranked by fewest missing).
- [ ] Output: combo name/pieces, owned vs. missing, effect description. Respect Spellbook's
      licensing/attribution.
- [ ] **Integration point 1:** standalone check against pasted decklist (pairs with Analyzer).
- [ ] **Integration point 2:** optional read-only signal during deck building (confirm with
      Master — recommend starting read-only to avoid entangling with deterministic fill logic).

**Open questions for Master:**
- Is v1 read-only (analysis/discovery) or should it feed into build-time selection?
- Attribution/display requirements from Spellbook's licensing.

### 3. Locked / Included Cards

Let the user specify cards that must appear in the generated deck regardless of budget,
theme, or archetype constraints — for pet cards or cards the user already owns.

- [ ] Add locked-card list input to Deck Builder flow (reuse decklist ingestion from Analyzer).
- [ ] Validate locked cards against commander color identity before build starts; reject or
      warn on illegal inclusions.
- [ ] Reserved as fixed slots before fill pass; counted toward `CoverageByRole` so fill pass
      doesn't over-provision.
- [ ] Confirm interaction with land count / Pass A / Pass B logic if locked card is a land.
- [ ] Confirm interaction with `RoleRelation` types — locked card with multiple roles must
      resolve correctly.

**Open questions for Master:**
- Budget semantics: excluded from total budget entirely, or deducted from remaining budget?
- Cap on number of locked cards (warn if alone they exceed 99, or hard build error)?

### Explicitly out of scope for Deck Analysis track

- Partner/Background commander support (separate, already on Master's roadmap).
- True collection import via persistent storage (no storage layer exists yet; locked-card input is a per-run manual workaround, not collection tracking).
- Any changes to Brawl builder, Duel Commander, or other format expansion work.

---

## Card Data Refresh (Cache Updates)

Scryfall bulk data is cached locally with a 24-hour max age. New cards released are not available until the app restarts and re-downloads. This creates a 24–48 hour lag between Scryfall publication and tool availability.

**Options:**
- **(a) Manual refresh button** — User clicks to force re-download if stale. Visible, deliberate.
- **(b) Background sync job** — Periodic task (e.g., nightly) checks Scryfall's `updated_at` timestamp, re-downloads if newer. Silent, automated.
- **(c) Hybrid (recommended)** — Show "last updated X hours ago" + optional manual refresh; background job runs nightly as fallback.

**Implementation tasks (option c):**
- [ ] `ICardRefreshService` interface: checks Scryfall `updated_at`, returns whether refresh happened and when
- [ ] `CardRefreshService` impl: compares cache timestamp with Scryfall manifest, downloads if newer, updates cache
- [ ] Wire into `ScryfallBulkClient` or as separate injected service
- [ ] Add "Last updated X hours ago" + "Refresh" button to Web UI (optional UX refinement)
- [ ] Background job (optional): scheduled task (using `ScheduledService` or similar) runs nightly
- [ ] Documentation: explain cache refresh behavior in `README.md`

**Scope:** ~2–3 hours for manual refresh (option a); add another 2–3 hours for background job (option b).
**Priority:** Low — new cards appear within acceptable lag; not blocking MVP.

---

## Deployment

Plan and execute the first production deployment of the app to a public host.

**Hosting decision (already made):** Single Hetzner VPS (EU data centers — GDPR-respecting,
cheap). Start on CX22 (~€4/month); scale vertically by resizing the VM if needed. Horizontal
scaling (multiple instances + sticky sessions + shared Data Protection keyring via managed
PostgreSQL) is a future concern only if the single VM proves insufficient.

**Hosting model:** Blazor Server. Confirmed — BYOK key lives in a scoped server-side service
and never reaches the browser. If the project ever moves to Blazor WASM, the BYOK design
must change (key would be browser-side).

**Tasks:**
- [ ] **Containerise the app**: write `Dockerfile` (multi-stage: SDK image to build, ASP.NET
      runtime image to serve) and `docker-compose.yml`. Mount a named volume for the ASP.NET
      Core Data Protection keyring so it survives container restarts and redeployments.
- [ ] **TLS termination**: run Caddy (or Nginx + Certbot) as a reverse proxy in the same
      Compose stack. Caddy handles Let's Encrypt certificate renewal automatically.
- [ ] **Secrets / config in production**: pass `Anthropic:ApiKey` (the fallback dev key, if
      kept), `TcgPlayer:AffiliateCode`, and Data Protection configuration via environment
      variables or a `.env` file excluded from source control. Document required env vars in
      `README.md`.
- [ ] **CI/CD**: GitHub Actions workflow — on push to `main`, build + test, build Docker image,
      push to a registry (GitHub Container Registry is free), SSH into the Hetzner VM and
      run `docker-compose pull && docker-compose up -d`.
- [ ] **Domain + DNS**: point a domain at the Hetzner IP. Caddy picks it up automatically
      for certificate issuance.
- [ ] **Scaling plan (document, don't implement yet)**: if load outgrows the single VM,
      the path is: (1) resize VM vertically, (2) add a second instance with sticky sessions
      and a managed Hetzner PostgreSQL for the shared Data Protection keyring
      (`PersistKeysToDbContext`). Document this in `README.md` so future-us doesn't have to
      rediscover it.

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
      cached except Plan/Synergy/Payoff), `LlmSelector` (user-selected model via `IClaudeClientFactory`,
      forced tool call, per-build rationale capture), `ClassificationCache`, `ClassificationPrompt`,
      `SelectionPrompt`.
- [x] **Agent — pipeline** — `RepairEngine` (deterministic CI-violation repair + result
      assembly), `DeckBuilder` (10-stage orchestration), 5 integration tests.
- [x] **Agent — BYOK** — `Authentication/` folder: `SessionApiKeyProvider` (scoped per-circuit,
      pre-populates from `Anthropic:ApiKey` config for dev), `IClaudeClientFactory` /
      `ClaudeClientFactory` (sole SDK-constructor seam), `IClaudeKeyTester` / `ClaudeKeyTester`
      (1-token probe), `MissingApiKeyException`, `ApiKeyRejectedException`, `ClaudeModels`
      (Haiku/Sonnet/Opus constants). `LlmClassifier` and `LlmSelector` updated to use factory;
      401 responses wrapped as `ApiKeyRejectedException`. All LLM-dependent services changed
      to Scoped; `ClassificationCache` remains Singleton.
- [x] **Agent — DI** — `ServiceCollectionExtensions.AddAgent()` (no longer takes `IConfiguration`
      — key comes from `SessionApiKeyProvider` which injects `IConfiguration` directly).
- [x] **Web — BYOK UI** — `ApiKeySettings.razor` component: connect / test key / disconnect,
      "Remember my key" checkbox (Data Protection-encrypted cookie, 30-day expiry), model picker
      (Haiku / Sonnet 5 / Opus 4.8 dropdown). `CommanderBuilder` gates the build form on key
      presence and handles `ApiKeyRejectedException` with a reconnect prompt. Cookie helpers
      (`setCookie`, `getCookie`, `deleteCookie`) added to `app.js`.
- [x] **Web — UI** — full Blazor UI: commander search, deck views (by role / by type / all cards),
      budget input & enforcement, export build report (`.md` download), raw decklist copy.
      Role buckets with coverage summary, runner-up panel, cut suggestions, archetype/theme picker
      with weight sliders, tune/custom theme form, bracket picker, color identity pips.
