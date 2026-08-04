# TODO — EdhDeckBuilder

A living list of deferred work. Check items off as they land.

---

## Multi-Provider LLM Support — Anthropic + Gemini LIVE (2026-07-08)

Anthropic and Google Gemini are both fully wired end-to-end. The Gemini adapters use a custom
REST client (not the OpenAI-compatible SDK path) posting directly to `generateContent` with a
structured `responseSchema`. See `README.md` and `CLAUDE.md` for the architecture; the archived
`TODO/Archive/GEMINI_IMPLEMENTATION_NOTES.md` records the original blocked state for context.

---

## OpenAI Direct Client — DONE (2026-07-10)

`OpenAiHttpLlmClient` posts to `api.openai.com/v1/chat/completions` with `Authorization: Bearer`.
System prompt maps to a system-role message; tools wrap in `{type:function}`; tool-call arguments
(a JSON string in OpenAI's format) are parsed into `JsonNode` so adapters receive a uniform
`LlmToolUseBlock`. `IsReasoningModel` gates temperature and `max_completion_tokens` for o-series
models. `OpenAiLlmClientFactory` implements `ILlmClientFactory`; classification always pins to
`gpt-4o-mini`. Wired into `ServiceCollectionExtensions.AddAgent` as a third case alongside
Anthropic and Gemini. Pricing added for `gpt-4o-mini`, `gpt-4o`, `o4-mini`, `o3`. `KeyTester`
upgraded to a real 1-token probe. 401/403 → `ApiKeyRejectedException`; quota 429s →
`QuotaExceededException` (all three providers). `InternalsVisibleTo` added to the Agent `.csproj`
so `BuildRequestJson`/`ParseResponse` are testable. 9 new unit tests; 337 total, all green.

---

## Bugs to Investigate

### Bracket estimation accuracy — to investigate (2026-08-03)

**Symptom:** Bracket estimates reported as off in manual testing. The current `BracketEstimator`
uses only Tutor count (primary signal) and Land+Protection counts (secondary). This misses
several important signals used by the official Command Zone bracket system (CEDH staples,
card-specific overrides, combo presence, fast mana like Mana Crypt/Mana Vault).

**Next steps:**
- Compare estimator output vs. the Command Zone bracket system on 5–10 real decklists.
- Check whether Commander Spellbook's `estimate-bracket` endpoint would be a better signal
  source than the hand-rolled heuristic.
- Identify which card signals (fast mana, hard stax, infinite combos) are missing from
  `BracketEstimator.cs` and add them or replace with an API-backed approach.

---

### LLM Classifier returns all Unmatched on cold cache (2026-07-07) — RESOLVED

**Root cause (confirmed via log analysis 2026-07-09):** Output token truncation. When a forced
tool call hits `max_tokens`, Anthropic returns `input: {}` (empty object) rather than partial
JSON. `ParseResponse` correctly logs "Tool response missing 'classifications' key" and returns
`[]`, but silently — no retry, and the caller defaults every pool card to `Unmatched`.

**Evidence from session logs:**
- `classification_session_2026-07-07_08-59-33.json`: batch size 100, `max_tokens=4096`. Every
  pool batch shows `OutputTokens=4096, ToolInputLength=2, ToolInputSample="{}"` — all truncated.
- `classification_session_2026-07-09_06-51-13.json`: batch size 30, `max_tokens=4096`. Three
  batches still hit exactly 4096 tokens and return `{}` (0 classifications).
- Sessions from 2026-07-09 07:13 onward: batch size 30, `max_tokens=8192`. Zero failures.
  Highest observed output: 4657 tokens (well under the new ceiling).

**Fixes applied:**
1. `ClassifierMaxOutputTokens` raised from 4096 → 8192 (sufficient margin for current usage).
2. `LlmClassifier.CallLlmAsync` now checks `response.StopReason == "max_tokens"` and
   recursively splits the batch in half before retrying, so any future truncation recovers
   automatically without silently dropping cards.
3. `ParseResponse` logs a `Warning` (not just `Information`) when `dtos.Count == 0`.
4. `ClassificationResponseLogger` now records `StopReason` in the per-session JSON files.

**Residual risk:** None anticipated. Peak output with reasoning enabled is ~4657 tokens against
an 8192 ceiling. The retry logic covers any future batch that exceeds the limit.

---

### Partner pair data mismatches in EDHREC (2026-07-08) — RESOLVED (2026-07-10)

**Root cause (confirmed via cached `partners.json` inspection):** EDHREC's `partnerwith`
cardlist encodes each pair as a single entry with the combined name `"Card1 // Card2"` —
e.g. `"Alisaie Leveilleur // Alphinaud Leveilleur"`. The old code used `ExtractSequentialPairs`
which paired entry[0] with entry[1], entry[2] with entry[3], etc. — producing 10 wrong
cross-pairings (e.g. pairing Alisaie+Alphinaud _entry_ with Amy+Rory _entry_) instead of
splitting within each entry.

**Fixes applied:**
1. `PartnershipIndexBuilder.ExtractPairsFromCardlist`: `"partnerwith"` now calls
   `ExtractSplitNamePairs` which splits each entry name on ` // ` to get both card names.
   `ExtractSequentialPairs` removed (was only used for this case).
2. `EdhrecPartnerMapper.ExtractPartnerWithPairs`: same ` // ` split logic applied
   (was unused but had the identical bug).
3. `CardRepositoryTests.MockEdhrecClient`: updated the `partnerwith` fixture to use the real
   EDHREC format (`"Rograkh, Son of Rohgadh // Drana, Liberator of Zendikar"` as one entry)
   so the test now validates the correct parsing path.

All 327 tests pass. Any remaining "pair not found" debug logs after this fix indicate stale
Scryfall bulk data (newer sets not yet downloaded) — not a code defect.

---

## Commander Discovery — progress step display + component restructure — DONE (2026-07-10)

Progress wired end-to-end on both pages, component folder restructure complete.

**Progress model:** `DiscoveryProgress` record (`Stage` + optional `Detail`) as the typed
model. `ICommanderDiscovery.DiscoverAsync` and `IDeckBuilder.BuildAsync` both now take
`Func<T, Task>?` callbacks (not `IProgress<T>`) so callers can `await` each stage report.
All tab components call `StateHasChanged()` then `await Task.Yield()` inside the callback,
forcing a render before the pipeline resumes — ensuring each stage label appears in the UI
as it starts. Discovery: three stages ("Gathering candidates", "Ranking commanders" with
candidate count as detail, "Assembling results"). Builder: existing ten stages, unchanged.

**Timing fixes:** Two separate bugs corrected.
- _First stage not visible on the Builder page:_ both builder tabs now call
  `await InvokeAsync(StateHasChanged); await Task.Yield()` before `BuildAsync`, so the
  progress panel is rendered (all stages pending) before the pipeline touches the UI.
- _"Assembling results" never shown as done on the Discovery page:_ after `DiscoverAsync`
  returns, tabs explicitly mark the last stage complete and yield before `_isRunning = false`
  transitions to the results view.

**Component restructure:** `Components/Tabs/` folder removed. Tab components now live beside
their host pages: `Components/Pages/CommanderBuilder/` holds `GuidedCommanderBuilderTab` and
`CustomCommanderBuilderTab`; `Components/Pages/Discovery/` holds `GuidedDiscoveryTab` and
`CustomDiscoveryTab`. `BuildProgress` extended with `Title` and `CurrentStageDetail`
parameters; `_Imports.razor` updated accordingly.

**Color-fixing cap warning removed:** "Color-fixing land cap reached" no longer appears in
`CoverageWarnings`. The cap is still enforced; hitting it is expected normal behaviour, not
user-actionable. The floor warning ("stopped at 8-basic floor") is kept — it is actionable.

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

### EDHREC theme-specific tag endpoints

EDHREC provides theme-focused recommendation endpoints that filter to only cards aligned with a
specific theme. These can reduce filtering and improve pool quality for unusual theme combinations.

- [ ] Example: `https://json.edhrec.com/pages/commanders/{commander-slug}/{theme-name}.json`
- [ ] New method: `ISuggestionSource.GetThemeRecommendationsAsync(Card commander, WeightedTheme theme, ...)`
- [ ] In `GatherPoolAsync()`, try theme endpoints for each weighted theme before merging single-commander pools
- [ ] Fallback to single-commander pools if theme endpoints return null
- [ ] Cache by theme (e.g., `{commander-slug}-{theme-name}.json`)

**Benefit:** Unusual partner pairs with niche themes (e.g., Sephiroth + very rare synergy) often result in
underfilled decks because classification filters heavily. Theme-focused pools should yield more relevant,
better-classified cards.

**Scope:** ~2–3 hours (new `EdhrecClient` methods + `GatherPoolAsync()` refactor + theme detection logic).
**Status:** Deferred. Current partner-pair support is production-ready; this is an optimization for edge cases.

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

## Partially deferred features

### Gemini context caching

`GeminiHttpLlmClient` currently ignores `LlmRequest.EnableCaching`. Gemini caching is a separate
API call (not inline like Anthropic's `cache_control` blocks) and requires billing enabled.

**How it works:** POST `systemInstruction` + `responseSchema` to `/v1beta/cachedContents` →
get a `name` token → include `"cachedContent": name` in subsequent `generateContent` calls.
Cached reads cost ~25% of normal input; there is a storage charge (~$1/1M tokens/hour). TTL
is set per cache (1 min–1 hour).

**Implementation gaps before this can land:**

- [ ] Add `CreateCachedContentAsync` / `DeleteCachedContentAsync` to `GeminiRestClient`.
      The `cachedContent` name field must be added to the `GeminiRequest` record (replaces
      `systemInstruction`; the two are mutually exclusive in the request body).
- [ ] Introduce a build-session concept (extend `GeminiRateLimiter` or add a new
      `GeminiBuildSession` scoped service) to hold the 2–3 cache names (classifier schema +
      selector schema + commander schema) across the multiple `SendAsync` calls within one build.
- [ ] `GeminiHttpLlmClient.SendAsync`: on first call with `EnableCaching = true`, create the
      cached content and store the name; on subsequent calls, include it. Clean up (delete or
      let expire) after the build via `IAsyncDisposable` on the session service.
- [ ] Add `CachedContentTokenCount` to `LlmUsage` (Gemini name differs from Anthropic's
      `cache_read_input_tokens`) and add cached-read pricing tier to `ModelPricing`.
- [ ] Verify minimum cached-content size threshold is met (1 024–4 096 tokens depending on
      model); add graceful no-op fallback if the create call is rejected.

**Blocked on:** paid Gemini account — context caching is not available on the free tier
(same `RESOURCE_EXHAUSTED / limit: 0` gate as the billing issue already documented).

---

### Build result default tab — DONE (2026-07-10)

Default changed to "All Cards". Tab order is now All Cards → By Type → Coverage Report
(formerly "By Role"). `DeckResults.razor.cs`: `_view = DeckView.AllCards`.

---

### Must-include cards (locked 99 slots) — DONE (2026-07-10)

Users nominate cards that must appear in the generated deck regardless of LLM selection,
budget constraints, or cut suggestions. Implemented end-to-end across all layers.

**Budget decisions (confirmed):**
- Locked cards are excluded entirely from both per-card and total budget enforcement.
  A user may own an expensive card they want in a budget deck; budget must not block it.
- If locked cards fill all available slots (≥ 99 − commander count), the build button is
  disabled — same UX as no commander selected. The deck analyzer (when built) is the right
  place for users with a full decklist.

**Implementation summary:**
- `DeckSlot.IsLocked` (Core) and `CardSuggestion.IsLocked` (Agent) propagate the flag
  through the full pipeline into the UI.
- `BuildContext.LockedOracleIds` makes the locked set visible to `RepairEngine` and
  `FillEngine` without threading extra parameters.
- `BuildState.Commit(isLocked: true)` marks slots and guards `Remove` from ever evicting
  a locked card (even if called accidentally).
- Locked cards are classified via `ILlmClassifier` before the fill loop, then pre-committed
  to `BuildState` so their coverage reduces how much the greedy fill needs to fill.
- Reconciliation and color-fixing repair skip locked oracle IDs.
- Budget repair (`RepairBudgetExcess`) excludes locked cards from the enforcement total.
- `BuildBudgetWarnings` skips locked cards for per-card checks; uses non-locked sum for total.
- Cut suggestions exclude locked cards entirely (honest: no advisory label needed — they
  simply aren't candidates for cutting).
- `ILockedCardValidator` / `LockedCardValidator` resolves names via `ICardRepository`,
  returns `ValidCards`, `UnrecognizedNames` (blocking), and `WrongColorCards` (advisory).
- Both Builder tabs (Guided + Custom) have a "Must-Include Cards" textarea with a Validate
  button, error/warning display, and build-button gating.
- Deck views (All Cards, By Type, Coverage Report) show a 🔒 badge on locked cards.
- 11 new tests (5 pipeline integration + 5 validator unit + 1 mixed); 348 total, all green.

**Follow-up fixes (2026-08-03):**
- Textarea placeholder `&#10;` entities now rendered via `@("...\n...")` binding (was literal string).
- Budget badge in results header shows non-locked total as primary; includes "incl. locked" secondary badge.
- Over-budget badge and red highlight suppressed on locked cards in all three deck views.
- Coverage warnings moved to `RepairEngine.Assemble` so they always agree with the final `ActualCoverage`
  (previously emitted mid-pipeline before color-fixing and repair passes).
- Scryfall bulk-data API breaking change handled: `download_uri` removed by Scryfall; client now
  reads `jsonl_download_uri` and decompresses JSONL.gz into a plain JSON array on disk.

---

### LLM step progress reporting — sub-steps done (2026-07-10)

Named stages with correct timing done 2026-07-10. Sub-step detail now live for both slow stages:

- `ClassifyPool` fires `"N / M cards classified"` before the first batch (showing cache hits
  immediately) and after each 30-card LLM batch. `ILlmClassifier.ClassifyAsync` accepts an
  optional `Func<string, Task>? subProgress` threaded from `IDeckBuilder.BuildAsync` →
  `DeckBuilder.ClassifyPoolAsync` → `LlmClassifier`.
- `FillEngine.FillAsync` accepts the same optional callback and fires `"Selecting {Role} cards…
  (N / 9)"` before each `selector.SelectAsync` call.

Remaining deferred:

- [ ] Percentage bar in `BuildProgress.razor` — would require a structured
      `(string Stage, int? PercentComplete)` progress type; the 10 fixed stages plus ~13
      sub-steps give granular 0–100%. Deferred: the text counter is sufficient for now.

---

### Multi-role classification

Core and UI infrastructure complete. Edge-case tuning deferred:

- [ ] Context-aware classification: e.g. Jeska's Will behaves differently with vs. without
      the commander on board; depends on commander castability and deck context.
- [ ] Tune default coverage weights (Always=1.0, Modal=0.5, Transform=0.75 are currently
      baked into Core defaults; may need per-commander calibration once real builds are tested).

### BYOK — Scoped settings

Base implementation complete. Token/cost estimate is now live per provider
(`Instrumentation/ModelPricing.cs`) — no remaining stretch items in this area.

### Saved deck results

Base storage complete. Subscription-aware limits deferred:

- [ ] **Wire saved-result limit to subscription tier.** Currently hardcoded as
      `DeckResultStorage.DefaultMaxSavedResults = 3` in `EdhDeckBuilder.Web/Services/DeckResultStorage.cs`.
      The JavaScript function `saveDeckResult(key, value, maxResults)` already accepts the limit
      as a parameter — no JS changes needed. On the C# side, resolve the limit from a subscription
      or feature-flag service and pass it to `JS.InvokeVoidAsync("saveDeckResult", key, json,
      resolvedLimit)` in `GuidedCommanderBuilderTab.razor.cs` and `CustomCommanderBuilderTab.razor.cs`.
      The two call sites are the only places that need updating.

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
- [ ] Plain text decklist export for users who prefer to paste into Mass Entry
      themselves — shares the `CartLine` mapping with the buy button.
- [ ] Multi-retailer support (Card Kingdom etc.) — keep the builder interface shaped
      so a second provider can slot in without redesign.

---

## Deck Analysis & Enhancement Track  (new — three features)

See `TODO/TODO_new_features.md` for the full brainstorm session with design context and open questions.

### 1. Deck Analyzer — v2 DONE (2026-08-03)

Paste an existing decklist → classify it → coverage report → bracket estimate → role gaps.
Plain `1 Card Name` format only. Commander in a separate picker. No upgrade paths in v1.

**Architecture:**
- `DecklistParser` (Agent singleton) — parses plain text; skips headers, comments, blanks.
- `BracketEstimator` (static) — deterministic: primary signal is Tutor count, secondary Land+Protection.
- `DeckAnalyzer` (Agent scoped) — orchestrates resolve → classify → coverage → bracket → gaps.
- `IDeckAnalyzer` interface in Agent/Interfaces.
- `AnalyzedCard`, `RoleGap`, `DeckAnalysisResult` models.
- `/analyze` Blazor page with `CommanderPicker` reuse, `BuildProgress` for stages.
  Optional "What isn't working?" field accepted but deferred for v2 weighting.
- 25 new tests (13 `DecklistParserTests` + 9 `BracketEstimatorTests`); 373 total, all green.

**v2 complete (2026-08-04):**
- [x] **Budget upgrade paths**: `IDeckUpgrader` / `DeckUpgrader` — fetches EDHREC pool per commander,
      filters by color + budget, classifies candidates, then makes two LLM calls: one Haiku call to
      prioritize gaps by user feedback (if provided), and one selector-model call per gap to pick
      3 ranked add+cut pairs. Shown in the new "Upgrade Paths" tab. Single max-price-per-card input.
- [x] **User experience feedback weighting**: "What isn't working?" field now feeds `DeckUpgrader`
      as `userFeedback` — the Haiku gap-prioritization call reorders gaps to match the player's pain
      points before upgrade selection runs. Removed from `IDeckAnalyzer.AnalyzeAsync` (analysis stays
      objective).
- [x] **3-tab + 4th Upgrade Paths tab**: Coverage Report, All Cards, By Type, Upgrade Paths.
- [x] **Home page card**: Deck Analyzer added to 4-column grid.
- [x] **MDFC land credit**: `BuildRoleProfile()` injects secondary `CardRole.Land` for MDFCs.
- [x] **Prompt improvements**: Ramp now covers extra land drops; TargetedDisruption includes
      single-target combat denial; Protection includes protection from colors/types.

**v3 complete (2026-08-04):**
- [x] **Additional input formats**: `DecklistParser` now handles Arena/Moxfield `(SET) ###`
      set-code suffixes, Archidekt/MTGO `[SET]` bracket codes, Moxfield `#` section headers,
      count-suffixed section headers like `Creatures (24)`, and `SB:` sideboard lines.
      20 new parser tests; 396 total, all green.

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

### Explicitly out of scope for Deck Analysis track

- Partner/Background commander support (separate, already on Master's roadmap).
- True collection import via persistent storage (no storage layer exists yet; locked-card input is a per-run manual workaround, not collection tracking).
- Any changes to Brawl builder, Duel Commander, or other format expansion work.

---

## Potential upgrades

Features and enhancements worth considering for future iterations:

- [ ] **Opening-hand / curve simulation** — Sanity-check deck consistency by simulating
      opening hands and mana curves.
- [x] **Plain text decklist export** — Done (2026-08-03). "Copy Decklist" and "Download (.txt)"
      buttons in the export panel. `DeckReportExporter.ExportDecklist()` is the shared method;
      `downloadTextFile` in `app.js` now accepts an optional MIME-type parameter.
- [ ] **Multi-retailer support** — Extend affiliate linking beyond TCGPlayer (Card Kingdom, etc.)
      while keeping the builder interface slot-in-ready.

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

## Summary of completed work

All four projects compile; 373 tests pass.

**Core & Infrastructure:** Domain model, rules, templates, archetypes, themes, bracket system. Scryfall bulk client, EDHREC client (single commander + partner pairs), `SuggestionSource` merge. `CanBeCommander` extended for planeswalker-commanders. MDFC/DFC back-face data fully supported. Colorless basic land (Wastes).

**Agent pipeline:** `LlmClassifier` (Haiku, forced tool call, batched, cached except Plan/Synergy/Payoff), `LlmSelector` (user model, forced tool call, per-build rationale). `FillEngine` (greedy + reconciliation, max 50 iterations), `ColorFixingPass` (pip-demand scoring). Deterministic `RepairEngine` + `DeckBuilder` (12-stage pipeline). Multi-role classification: role profiles, secondary contributions, coverage accounting.

**Multi-provider LLM (Anthropic + OpenAI + Gemini):** Three provider-agnostic adapters (`LlmClassifier`, `LlmSelector`, `LlmCommanderSelector`) dispatch to one of three `ILlmClient` implementations via `ILlmClientFactory`. Anthropic path via `ClaudeHttpLlmClient` — direct HTTP to `api.anthropic.com/v1/messages`, no Anthropic C# SDK. OpenAI path via `OpenAiHttpLlmClient` — direct HTTP to `api.openai.com/v1/chat/completions`; maps tool calls to `{type:function}`, parses JSON-string arguments; gates temperature and `max_completion_tokens` for o-series reasoning models; classification pinned to `gpt-4o-mini`. Gemini path via `GeminiHttpLlmClient` wrapping `GeminiRestClient` — posts to `generateContent` with `responseSchema` structured output. `GeminiSchemas` translates our schema shape to Gemini's OpenAPI 3.0 subset. `GeminiRateLimiter` (Scoped) enforces per-circuit RPM pacing. All three clients map 401/403 → `ApiKeyRejectedException` and quota 429s → `QuotaExceededException` (no retry); transient 429s still retry with backoff. `KeyTester` probes Anthropic and OpenAI with a live 1-token call; Google is format-validated only.

**Cost accounting:** `Instrumentation/ModelPricing.cs` — per-model USD rates per 1M tokens for both providers. `UsageTracker` uses it for per-call rows and summary totals; mixed-provider runs tally correctly. Free-tier Gemini calls show the "what you'd pay on paid tier" estimate. `IUsageTrackerAware` marker interface removes type-specific dispatch — all three adapters implement it, `DeckBuilder` and `CommanderDiscovery` wire the tracker through it.

**BYOK & authentication:** `SessionApiKeyProvider` (scoped per-circuit) with triple-key storage (Anthropic / OpenAI / Google). `ClaudeHttpLlmClientFactory` (Anthropic HTTP seam), `GeminiLlmClientFactory` (Gemini REST client seam), both implementing `ILlmClientFactory`. `KeyTester` (1-token probe for Anthropic; format check for Google/OpenAI). 401/403 → `ApiKeyRejectedException` wrapping on both live provider paths. Data Protection-encrypted cookies (30-day expiry) for each provider plus a selected-model cookie; cookies win over `Provider:Default` in appsettings. Prompt caching implemented on the Anthropic path (`cache_control` on last tool definition); Gemini path ignores `EnableCaching`.

**Web UI:** Commander search + deck builder. Three deck views (by role / by type / all cards). Coverage summary, runner-up panel, cut suggestions. Budget input & enforcement (per-card + total). Archetype/theme picker with weight sliders; 29 themes + custom escape hatch. Bracket selection. Export build report (`.md` download). Color identity picker with exact-match option. Provider toggle (Anthropic / OpenAI / Google AI Studio) with per-provider help text and dynamic model dropdown.

**Commander Discovery:** Standalone `/discover` page with two tabs: Guided (LLM-assisted, archetype/theme-driven) and Custom (free-text strategy description). Ranked commander suggestions with art, rationale, and power level. Contiguous rank normalization in `BuildSuggestionsFromResults` — display is always 1..N regardless of what the model emits. Partner-pair support (all 8 variants: Partner, Partner with, Background, Friends Forever, Doctor's Companion, Survivors, Character Select, Father & Son). EDHREC partner index integrated. Deck builder pool gathering queries partner-pair endpoints with redirect handling and canonical caching. Graceful fallback to merged single-commander pools.

**Deck results & logging:** Saved locally (3-max, Data Protection cookie). Token usage logging with per-call table + summary total across all discovery and build flows, per-provider pricing.

**Deck Download:** Markdown export with header, role buckets (with rationale), runner-ups, coverage summary, raw decklist (ready to paste).

**Build progress — sub-step detail:** `ClassifyPool` shows `"N / M cards classified"` (updating per 30-card batch, seeded immediately with cache-hit count). `FillEngine` shows `"Selecting {Role} cards… (N / 9)"` before each selector call. Both stages surface their detail via `CurrentStageDetail` in `BuildProgress.razor`.

**Deck Analyzer (v1–v3):** `/analyze` page — paste decklist + pick commander → classify 99 cards via `ILlmClassifier`, compute coverage by role, estimate bracket deterministically (`BracketEstimator`), surface role gaps vs. balanced baseline. `DecklistParser` handles plain `1 Card Name` plus Arena `(SET) ###`, Archidekt/MTGO `[SET]`, Moxfield `#` headers, count-suffixed section headers, and `SB:` sideboard lines. MDFCs resolved by front-face name only. Commanders included in the pasted 99 are de-duplicated automatically. **Upgrade Paths tab** (`IDeckUpgrader` / `DeckUpgrader`): two-LLM-call pipeline per gap — cheap Haiku/Lite prioritization of gaps by user feedback, then per-gap add+cut suggestions from the selected model, validated against the EDHREC pool whitelist and the current decklist.

**Tests:** 399 tests (Core rules, archetypes, templates, budget, discovery, partnership index, selection, fill engine, color fixing, repair, BYOK, integration, deck analyzer, upgrade parser). All green.
