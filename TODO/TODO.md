# TODO — EdhDeckBuilder

A living list of deferred work. Check items off as they land.

---

## Features

### Multi-role classification

Core and UI infrastructure are complete. The following enhancements remain:

- [ ] **User-selectable multi-role classification** — add an opt-in toggle (clearly labelled
      as slower and more expensive) that allows the LLM to assign 2+ roles per card. When
      disabled (default), classification assigns one primary role per card. When enabled,
      secondary overlaps are also resolved, expanding coverage accounting at the cost of
      increased classification latency and token spend.
- [ ] Context-aware classification: e.g. Jeska's Will behaves differently with vs. without
      the commander on board; depends on commander castability and deck context.
- [ ] Tune default coverage weights (Always=1.0, Modal=0.5, Transform=0.75 are currently
      baked into Core defaults; may need per-commander calibration once real builds are tested).

---

### Subscription Tier

- [ ] **Define subscription tiers** (e.g. Free, Pro) and their corresponding feature limits —
      saved builds, saved analyses, and future gated features such as multi-role classification.
- [ ] **Wire saved-result limit to subscription tier.** Currently hardcoded as
      `DeckResultStorage.DefaultMaxSavedResults = 3` in `EdhDeckBuilder.Web/Services/DeckResultStorage.cs`.
      The JavaScript function `saveDeckResult(key, value, maxResults)` already accepts the limit
      as a parameter — no JS changes needed. On the C# side, resolve the limit from a subscription
      or feature-flag service and pass it to `JS.InvokeVoidAsync("saveDeckResult", key, json,
      resolvedLimit)` in `GuidedCommanderBuilderTab.razor.cs` and `CustomCommanderBuilderTab.razor.cs`.

---

### TCGPlayer affiliate linking

A "Buy this deck on TCGplayer" action on a finished deck. Sends the full decklist into
TCGplayer's Mass Entry cart tool, tagged with an affiliate code.

See `TODO/TCGPLAYER_AFFILIATE_LINKING.md` for the full design spec.

**Prerequisites (non-code actions — must happen before commission tracking works):**
- [ ] **WotC Fan Content Policy check**: confirm the policy permits monetization for a tool
      like this before enabling any affiliate links.
- [ ] **Apply to TCGplayer's affiliate program** via Impact (impact.com).
- [ ] **EDHREC ToS review** — the current client hits EDHREC's public JSON endpoints
      (`json.edhrec.com`). Their ToS permits fan tools with attribution but prohibits
      commercial scraping. If the app grows or monetises, get explicit written permission
      from EDHREC before proceeding.
- [ ] **WotC Fan Content Policy compliance check** — confirm the tool's use of card names
      and Scryfall data falls within the policy before adding any monetisation feature.
      Resolve the item above first.

**Implementation tasks:**
- [ ] `TcgPlayerLinkOptions` (singleton from config): holds `AffiliateCode` and `Medium`.
- [ ] `TcgPlayerMassEntryLinkBuilder` (scoped): `BuildGetUrl` for single-card/preview links;
      `BuildPostForm` (returns action URL + raw card list value) for full decks.
- [ ] Map `DeckBuildResult` → `IReadOnlyList<CartLine>` at the call site in the Web layer.
- [ ] `BuyDeckButton.razor`: renders a native `<form method="post">` with hidden inputs.
      `target="_blank"` to open the cart in a new tab. Do not use `fetch` — CORS blocks it.
- [ ] DI registration: `TcgPlayerLinkOptions` as singleton; `TcgPlayerMassEntryLinkBuilder` scoped.
- [ ] Plain text decklist export for users who prefer to paste into Mass Entry themselves.
- [ ] Multi-retailer support (Card Kingdom etc.) — keep the builder interface slot-in-ready.

---

---

## Potential upgrades

- [ ] **EDHREC budget/price filtering** — The builder lets users set a per-card price cap
      and total budget, but these constraints are applied after the EDHREC pool is gathered
      (cards are filtered out in `FilterPool`). EDHREC exposes budget-specific recommendation
      pages (e.g. `/commanders/{slug}/budget`) that return cards already pre-filtered to lower
      price points.
  - [ ] Investigate EDHREC's budget URL variants (e.g. `/budget`, `/ultra-budget`) and
        whether they return a meaningfully different card set.
  - [ ] If they do, extend `ISuggestionSource` with a budget-aware overload and call the
        budget endpoint when the user has set a per-card price cap below a threshold (e.g. $2).
        This would improve pool quality for budget builds rather than just filtering out
        expensive cards after the fact.

- [ ] **Opening-hand / curve simulation** — Sanity-check deck consistency by simulating
      opening hands and mana curves.

- [ ] **Multi-retailer support** — Extend affiliate linking beyond TCGPlayer (Card Kingdom, etc.)
      while keeping the builder interface slot-in-ready.

---

## Future

### Historic Brawl on MTG Arena

Historic Brawl is 100-card singleton, same physical count as Commander, but with meaningful
differences: 1-vs-1, an **eternal** card pool, and two distinct queues.

Key differences from EDH to plan around:

- **Card pool:** Scryfall exposes `legalities.historicbrawl`. Card ingestion needs a separate
  flag. The pool is eternal — no rotation, just a separate ban list.
- **Two queues — ranked vs. casual:** the builder should know which queue the user is targeting
  and adjust the `DeckTemplate` baseline and selector guidance accordingly.
- **1v1 meta:** Board wipes are weaker, reactive spells matter more. The `DeckTemplate` targets
  need a Brawl-specific baseline.
- **Sources:** EDHREC does not cover Historic Brawl well; the existing client would need
  extension to Brawl URL path variants. No sanctioned competitive popularity source exists
  (Untapped.gg is commercial).
- **Commander legality:** Any legendary creature or planeswalker legal in the format can be the
  commander. `CanBeCommander` logic needs a format-aware variant.
- **Arena-only cards:** Alchemy and Historic Anthology cards exist only on Arena. The card model
  may need an `IsArenaOnly` flag.

**Owner decisions required before implementation:**

- [ ] **Historic Brawl competitive popularity gap.** No sanctioned open source exists.
      Decide whether to accept EDHREC + Scryfall alone for Brawl recommendations.

**Implementation tasks:**

- [ ] Define `FormatProfile` (or equivalent) in Core: legality check, deck size, commander
      count, baseline `DeckTemplate`, and queue/power-level model.
- [ ] Add `historicbrawl` legality flag and `IsArenaOnly` flag to `Card` ingestion.
- [ ] Extend `EdhrecClient` to resolve Brawl URL path variants alongside existing Commander paths.
- [ ] Create Brawl `DeckTemplate` baselines — ranked and casual variants.
- [ ] Update `IDeckBuilder.BuildAsync` (or a new `IBrawlDeckBuilder`) to accept a
      `FormatProfile` and route to the correct template, sources, and legality rules.
- [ ] Test with a known strong Historic Brawl commander (e.g. Atraxa, Raffine, Sheoldred).

---

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
      `GeminiBuildSession` scoped service) to hold the 2–3 cache names across calls in one build.
- [ ] `GeminiHttpLlmClient.SendAsync`: on first call with `EnableCaching = true`, create the
      cached content and store the name; on subsequent calls, include it.
- [ ] Add `CachedContentTokenCount` to `LlmUsage` and add cached-read pricing tier to `ModelPricing`.
- [ ] Verify minimum cached-content size threshold is met; add graceful no-op fallback.

**Blocked on:** paid Gemini account — context caching is not available on the free tier.

---

## Summary of completed work

All four projects compile; 441 tests pass. All items below are complete.

**Core & Infrastructure:** Domain model, rules, templates, archetypes, themes, bracket system. Scryfall bulk client (JSONL.gz format), EDHREC client (single commander + partner pairs), `SuggestionSource` merge. `CanBeCommander` extended for planeswalker-commanders. MDFC/DFC back-face data fully supported. Colorless basic land (Wastes). Commander Spellbook client (`find-my-combos` + `estimate-bracket`, SHA256-cached). `ComboPoolSource` injects near-miss combo pieces into the builder pool.

**Agent pipeline:** `LlmClassifier` (Haiku, forced tool call, batched at 30 cards, cached except Plan/Synergy/Payoff), `LlmSelector` (user model, forced tool call, per-build rationale). `FillEngine` (greedy + reconciliation, max 50 iterations), `ColorFixingPass` (pip-demand scoring). Deterministic `RepairEngine` + `DeckBuilder` (12-stage pipeline). Multi-role classification: role profiles, secondary contributions, coverage accounting. Build progress: named stages with sub-step detail for `ClassifyPool` and `FillEngine`.

**Multi-provider LLM (Anthropic + OpenAI + Gemini):** Three provider-agnostic adapters (`LlmClassifier`, `LlmSelector`, `LlmCommanderSelector`) dispatch via `ILlmClientFactory`. Anthropic path via `ClaudeHttpLlmClient` (direct HTTP). OpenAI path via `OpenAiHttpLlmClient` (direct HTTP; `{type:function}` tools; reasoning model gating for o-series). Gemini path via `GeminiRestClient` (direct REST to `generateContent`; `GeminiSchemas`; `GeminiRateLimiter` per circuit; retry with backoff). All three clients map 401/403 → `ApiKeyRejectedException`; quota 429s → `QuotaExceededException`. Prompt caching on Anthropic path.

**Cost accounting:** `ModelPricing` — per-model USD rates for all three providers. `UsageTracker` for per-call rows and summary totals. `IUsageTrackerAware` marker removes type-specific dispatch.

**BYOK & authentication:** `SessionApiKeyProvider` (scoped per-circuit) with triple-key storage. `KeyTester` (1-token probe for Anthropic/OpenAI; format check for Gemini). Data Protection-encrypted cookies (30-day expiry). `ApiKeyRejectedException` / `QuotaExceededException` surface to the UI.

**Web UI:** Commander search + deck builder (Guided + Custom tabs). Three deck views (All Cards / By Type / By Mana Value / Coverage Report). Coverage summary, runner-up panel, cut suggestions. Budget input & enforcement (per-card + total). Archetype/theme picker with weight sliders; themes + custom escape hatch. Bracket selection. Export build report (`.md` download + clipboard copy). Color identity picker with exact-match option. Provider toggle (Anthropic / OpenAI / Google AI Studio) with per-provider help text and dynamic model dropdown. Must-include (locked) cards: textarea + validate + locked badge in all views; locked cards excluded from budget enforcement.

**Commander Discovery:** `/discover` page with Guided and Custom tabs. Ranked commander suggestions with art, rationale, and power level. Partner-pair support (all 8 variants). EDHREC partner index integrated. Deck builder pool gathering queries partner-pair endpoints with redirect handling and canonical caching. Graceful fallback to merged single-commander pools.

**Deck results:** Saved locally (3-max, Data Protection cookie). Token usage logging per-call + summary total across all flows, per-provider pricing. Build result default tab: All Cards.

**Deck Analyzer (v1–v3):** `/analyze` page — paste decklist + pick commander → classify via `ILlmClassifier` → coverage by role → bracket estimate (Commander Spellbook `estimate-bracket`) → role gaps. `DecklistParser` handles plain `1 Card Name` plus Arena/Moxfield/Archidekt/MTGO/Moxfield-header/SB: formats. **Upgrade Paths tab** (`DeckUpgrader`): two-LLM-call pipeline per gap — cheap Haiku/Lite gap prioritization, then per-gap add+cut suggestions validated against EDHREC pool. **Combo Finder:** `IComboFinder` / `ComboFinder` calls `find-my-combos` on demand; Combos tab shows complete combos and near-misses. **Upgrade Paths + Combos tabs also on DeckResults page** (independent price input; deduplication fix for `ToDictionary`). **Mana Curve:** `ManaCurveChart` shared component (bar chart + spline overlay + type breakdown + collapsible CMC buckets).

**Saved Results:** `AnalysisResultStorage` (parallel to `DeckResultStorage`) saves analysis results to `localStorage`. `DeckAnalyzerPage` saves on every successful analysis and restores via `?load={id}`. `/saved` page lists all saved builds and analyses (newest first).

**Popular EDHREC themes:** `ISuggestionSource.GetPopularThemesAsync` fetches `panels.taglinks` from the EDHREC commander page and returns a ranked list of `(Slug, Name, Count, Theme?, Archetype?)` tuples. Commander Discovery shows the top 6 as badge chips on each suggestion card; clicking "Build this deck" with no user-selected themes presets the top known theme in the builder link. Deck Analyzer shows the top 8 after analysis completes. Badge color is primary (blue) for known `Theme` values, known `Archetype` slugs (aggro/midrange/control/combo), and any creature-type tribal slug matched against the `CreatureTypeCatalog`; secondary (grey) for unrecognised tags.

**Tribal theme picker:** Replaced the non-interactive `<datalist>` with a Blazor combobox — text input backed by a filtered dropdown from the `CreatureTypeCatalog` (Scryfall creature type API → pluralised → sorted). Custom values are still allowed; the dropdown just guides spelling and singular-vs-plural. `@onmousedown:preventDefault` on each option prevents the blur-before-click race.

**Duplicate card fix:** `FillEngine.FillAsync` inner loop now guards against the same `OracleId` appearing twice in a ranked LLM response. `BuildState.Commit` also has a defensive early-return for the same case.

**Card data refresh:** `ICardRefreshService` / `CardRefreshService` (Infrastructure) expose `GetLastRefreshed()` and `ForceRefreshAsync()` via `ScryfallBulkClient`. `ScryfallRefreshBackgroundService` (Web, `BackgroundService`) runs on a configurable interval (default 7 days; override via `Scryfall__RefreshInterval` env var), computes the initial delay from cache file age so restarts don't re-download unnecessarily. `CardDataStatus` component in the layout top bar shows "Cards: X days ago" on every page; a manual "Refresh" button is shown in Development mode only. Scheduling logic extracted to `ScryfallRefreshScheduler` (pure static function, tested independently).

**Tests:** 441 tests (Core rules, archetypes, templates, budget, discovery, partnership index, selection, fill engine, color fixing, repair, BYOK, integration, deck analyzer, upgrade parser, combo pool source, creature type catalog pluralisation and slug matching, Scryfall refresh scheduling). All green. Web project exposes internals to the test project via `InternalsVisibleTo`.

**Component refactoring:** Display helpers (`SecondaryBadge`, `BadgeInfo`, `BracketTagLabel`, `BracketTagCss`) centralised in `CardRoleDisplay`. `DeckBuildStages` constant extracted to `BuildRequestFactory`. Shared components extracted to `Components/Shared/`: `UpgradePathsPanel`, `CombosPanel`, `CardsByTypeList` (with `IsLocked` added to `AnalyzedCard`), `LockedCardInput` (with `LockedCardState` record), and `RoleTargetEditor` (replaces hand-crafted `RenderFragment` builder API code). `CoverageReportPanel` (in `Components/Results/`) owns the full Coverage Report tab — summary table, role buckets, basic lands, runner-ups — parameterised with `RenderFragment` slots (`SummaryHeaderActions`, `SummaryBodyPrefix`, `TargetCellContent`, `Alerts`) for host-specific customisation. Both `DeckResults` and `DeckAnalyzerPage` are now thin nav-tab shells.

**Public documentation & in-app help:** Contextual, inline documentation rather than a separate /docs page. BYOK inline help in the API key dropdown — per-provider steps, practical tips, and a collapsible privacy disclosure. How-It-Works info modals (ℹ button in each page header) on Commander Builder, Commander Discovery, and Deck Analyzer covering each feature's pipeline, AI steps, and key concepts in plain language. `InfoIconButton` shared component. `CardRoleGlossary` is the single source of truth for all 12 role definitions — both the classification prompt's `## Roles` section and the UI tooltips are built from it. `RoleHelpTip` component renders a `?` badge next to role names in the Coverage Report summary table and role bucket headers; hovering shows the exact definition the classifier used. `AiLimitationsNotice` component shown near all five submit buttons. Privacy disclosure in the API key dropdown, on the Saved Results page subtitle, and in the app footer. Sticky footer on all pages: privacy statement, data source attribution (Scryfall CC BY 4.0, EDHREC, Commander Spellbook), and WotC fan content disclaimer. Default Coverage Report tab now first in deck results.

**Deployment:** Hetzner CX23 VPS + Docker Compose + Caddy reverse proxy (automatic TLS). GitHub Actions CI/CD: test → build → push to `ghcr.io/jacob-winther-solutions/edh-deck-builder` → SSH deploy. Live at https://deckconsult.winther-solutions.dk. Full setup in `TODO/DEPLOYMENT.md`.

**EDHREC theme-specific tag endpoints:** `IEdhrecClient.GetCommanderThemePageAsync` (`/commanders/{slug}/{themeSlug}.json`) and `GetTagsPageAsync` (`/tags/{themeSlug}.json`) implemented. `SuggestionSource.GetCommanderThemeRecommendationsAsync` and `GetTagsAsync` map pages to `CardCandidate` lists. `DeckBuilder.GetThemePoolAsync` fires all `(commander × theme)` and per-theme tags-page requests in parallel, merges into `edhrecPool` before classification.

**Deck Strategy description:** `DeckAnalyzer` collects Plan-primary cards after classification and makes a single lightweight LLM call (`describe_plan` forced tool, Haiku/lite model, 512 tokens) to produce a 2–3 sentence natural-language description of the deck's win condition. Shown as a "Deck Strategy" callout at the top of the Coverage Report tab and included in exported analysis markdown. Gemini path supported via `GeminiSchemas.BuildPlanDescriptionSchema`.
