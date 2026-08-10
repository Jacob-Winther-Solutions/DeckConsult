# TODO — EdhDeckBuilder

A living list of deferred work. Check items off as they land.

---

## Additional card sources — EDHREC Brawl  (prerequisite for Brawl / Duel Commander)

See `TODO/DATA_SOURCES.md` for the full spec, verified API details, and the excluded-sources
list. Aetherhub and mtgrocks are explicitly excluded. Commander Spellbook (`find-my-combos` +
`estimate-bracket`) is fully implemented via `CommanderSpellbookClient`. Remaining sources:

- **EDHREC — extend to Brawl paths** — the existing client already handles Commander; extend
  it to resolve the Brawl URL variants for Historic Brawl support.
- **MTGJSON** — optional; only adopt if offline bulk card data or preconstructed deck seed
  lists become a goal. Defer until there is a concrete use case.

**Owner decisions required before implementation:**

- [ ] **Adopt MTGJSON now or defer.**
- [ ] **Historic Brawl competitive popularity gap.** No sanctioned open source exists
      (Untapped.gg is commercial). Decide whether to accept EDHREC + Scryfall alone for Brawl.

**Implementation tasks:**

- [ ] Extend `EdhrecClient` to resolve Brawl URL path variants alongside existing Commander paths.


---

## Multi-format support  (new formats — depends on Additional sources above)

### Historic Brawl on MTG Arena

Historic Brawl is 100-card singleton, same physical count as Commander, but
with meaningful differences: 1-vs-1, an **eternal** card pool, and two distinct queues.

Key differences from EDH to plan around:

- **Card pool:** Scryfall exposes `legalities.historicbrawl`. Card ingestion needs a separate
  flag. The pool is eternal — no rotation, just a separate ban list.
- **Two queues — ranked vs. casual:** the builder should know which queue the user is targeting
  and adjust the `DeckTemplate` baseline and selector guidance accordingly.
- **1v1 meta:** Board wipes are weaker, reactive spells matter more. The `DeckTemplate` targets
  need a Brawl-specific baseline.
- **Sources:** EDHREC does not cover Historic Brawl.
- **Commander legality:** Any legendary creature or planeswalker legal in the format can be the
  commander. `CanBeCommander` logic needs a format-aware variant.
- **Arena-only cards:** Alchemy and Historic Anthology cards exist only on Arena. The card model
  may need an `IsArenaOnly` flag.

- [ ] Define `FormatProfile` (or equivalent) in Core: legality check, deck size, commander
      count, baseline `DeckTemplate`, and queue/power-level model.
- [ ] Add `historicbrawl` legality flag and `IsArenaOnly` flag to `Card` ingestion.
- [ ] Implement `AetherhubBrawlSource` and `MtgdecksBrawlSource` in Infrastructure.
- [ ] Create Brawl `DeckTemplate` baselines — ranked and casual variants.
- [ ] Update `IDeckBuilder.BuildAsync` (or a new `IBrawlDeckBuilder`) to accept a
      `FormatProfile` and route to the correct template, sources, and legality rules.
- [ ] Test with a known strong Historic Brawl commander (e.g. Atraxa, Raffine, Sheoldred).

### Duel Commander (French Commander)

Duel Commander is 1v1 100-card singleton with a separate, more aggressive banlist than EDH.

- [ ] Research Duel Commander legality — Scryfall does not expose a dedicated legality flag.
- [ ] Create Duel Commander `DeckTemplate` baseline — tuned for 1v1 faster pace.
- [ ] Update `FormatProfile` to support Duel Commander legality and bracket/queue model.

### Pauper EDH (Pauper Commander)

Pauper EDH requires all cards (including commander) to have been printed at common rarity.

- [ ] Integrate Scryfall's rarity data — cards must be `rarity == "common"`.
- [ ] Confirm banlist source (if any) — Pauper EDH is community-managed.
- [ ] Create Pauper EDH `DeckTemplate` baseline.
- [ ] Update `FormatProfile` to support Pauper EDH legality.

### Peasant Commander

Peasant Commander allows up to 5 uncommon cards in the deck.

- [ ] Research Peasant Commander rules and banlist source.
- [ ] Implement rarity-counting logic — track uncommons per deck, flag when exceeding 5.
- [ ] Create Peasant Commander `DeckTemplate` baseline.
- [ ] Update `FormatProfile` to support Peasant Commander legality and rarity constraints.

---

## Partially deferred features

### Adding more themes

New themes follow the same pattern: add the enum value, a `ThemeProfile` with `Adjustments` in
`ThemeLibrary`, a slug entry in `EdhrecThemeSlugger`, and a `ThemeGroup` assignment. The UI
groups themes by category with section headers (see `ThemePicker.razor`).

Verified against EDHREC `/tags` page (2026-08-07). Creature-type tags are excluded (covered
by `Theme.Tribal`). Deck counts shown for reference. All slugs verified on EDHREC.

**Tier 1 — >15 k decks (implement first):**
- [x] Burn (`burn`, 63 k) — direct damage as win condition
- [x] Sacrifice (`sacrifice`, 40 k) — broader sacrifice payoffs beyond Aristocrats drain
- [x] Auras (`auras`, 35 k) — aura-matters, distinct from Enchantress (which is about enchantments drawing cards)
- [x] Treasure (`treasure`, 50 k) — treasure token generation/payoffs
- [x] Legends (`legends`, 34 k) — legendary-matters (Sisay, Jodah, etc.)
- [x] Discard (`discard`, 33 k) — madness, hand-emptying synergies
- [x] Clones (`clones`, 30 k) — copy/clone creatures
- [x] Landfall (`landfall`, 24 k) — landfall trigger payoffs
- [x] Group Slug (`group-slug`, 23 k) — everyone takes damage/loses life
- [x] Historic (`historic`, 23 k) — artifacts + legends + sagas matter
- [x] Extra Combats (`extra-combats`, 21 k) — take multiple combat steps
- [x] Theft (`theft`, 20 k) — steal opponents' permanents or spells
- [x] Self-Mill (`self-mill`, 20 k) — fill own graveyard (distinct from Mill which targets opponents)
- [x] Birthing Pod (`birthing-pod`, 17 k) — creature-chain tutor strategies
- [x] Forced Combat (`forced-combat`, 16 k) — goad and forced-attack effects
- [x] Vehicles (`vehicles`, 16 k) — vehicle-matters
- [x] X Spells (`x-spells`, 16 k) — large-X spell strategies
- [x] Commander Matters (`commander-matters`, 14 k) — build around commander mechanics

**Tier 2 — 10–15 k decks:**
- [x] Exile (`exile`, 13 k) — exile zone manipulation (Prosper, Gonti, etc.)
- [x] Cascade (`cascade`, 12 k) — cascade trigger chains
- [x] Hatebears (`hatebears`, 12 k) — creature-based disruption/taxing
- [x] Toughness Matters (`toughness-matters`, 11 k) — toughness as power or payoff
- [x] Spell Copy (`spell-copy`, 10 k) — fork/copy spells
- [x] Extra Turns (`extra-turns`, 10 k) — time-walk effects

**Tier 3 — 5–10 k decks:**
- [x] ETB (`etb`, 9 k) — enters-the-battlefield triggers (broader than Blink)
- [x] Energy (`energy`, 9 k) — energy counter generation and payoffs
- [x] Ninjutsu (`ninjutsu`, 9 k) — ninjutsu mechanic strategies
- [x] Sagas (`sagas`, 8 k) — saga-matters payoffs
- [x] Attack Triggers (`attack-triggers`, 7 k) — on-attack trigger payoffs
- [x] Clues (`clues`, 6 k) — investigate/clue token generation
- [x] Food (`food`, 6 k) — food token generation
- [x] Monarch (`monarch`, 6 k) — monarch mechanic

**UI grouping** (section headers in `ThemePicker.razor`, search still spans all):
- **Permanents:** Artifacts, Equipment, Enchantress, Auras, Vehicles, Sagas, Superfriends, Tokens, Treasure
- **Graveyard:** Reanimator, Graveyard, Self-Mill, Aristocrats, Sacrifice, Birthing Pod
- **Spells:** Spellslinger, Storm, Wheels, Discard, Spell Copy, X Spells, Cascade, Cycling, Extra Turns
- **Lands:** Lands, Landfall, Big Mana
- **Counters:** +1/+1 Counters, -1/-1 Counters, Counters Matter, Proliferate, Energy, Infect
- **Combat:** Voltron, Burn, Extra Combats, Attack Triggers, Forced Combat, Theft, Ninjutsu
- **Politics & Control:** Stax, Pillowfort, Hatebears, Group Hug, Group Slug, Chaos, Monarch, Mill
- **Synergy:** Blink, ETB, Commander Matters, Legends, Exile, Historic, Clones, Toughness Matters, Lifegain, Clues, Food
- **Tribal:** (special — with creature-type picker)

### EDHREC budget/price filtering

The builder lets users set a per-card price cap and total budget, but these constraints
are applied after the EDHREC pool is gathered (cards are filtered out in `FilterPool`).
EDHREC exposes budget-specific recommendation pages (e.g. `/commanders/{slug}/budget`)
that return cards already pre-filtered to lower price points.

- [ ] Investigate EDHREC's budget URL variants (e.g. `/budget`, `/ultra-budget`) and
      whether they return a meaningfully different card set.
- [ ] If they do, extend `ISuggestionSource` with a budget-aware overload and call the
      budget endpoint when the user has set a per-card price cap below a threshold (e.g. $2).
      This would improve pool quality for budget builds rather than just filtering out
      expensive cards after the fact.

### EDHREC theme selection in Custom Builder

- [ ] Allow users to pick a theme directly from EDHREC in the Custom commander builder tab.
      Fetch popular theme tags from the selected commander's EDHREC page (`panels.taglinks`)
      and surface them as clickable suggestions alongside the built-in theme picker.
      Clicking a tag pre-selects the matching built-in `Theme` (if one exists) or creates a
      lightweight custom theme derived from the tag name and EDHREC slug.

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

### LLM step progress — percentage bar

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

---

### Saved results — subscription tier limits

- [ ] **Wire saved-result limit to subscription tier.** Currently hardcoded as
      `DeckResultStorage.DefaultMaxSavedResults = 3` in `EdhDeckBuilder.Web/Services/DeckResultStorage.cs`.
      The JavaScript function `saveDeckResult(key, value, maxResults)` already accepts the limit
      as a parameter — no JS changes needed. On the C# side, resolve the limit from a subscription
      or feature-flag service and pass it to `JS.InvokeVoidAsync("saveDeckResult", key, json,
      resolvedLimit)` in `GuidedCommanderBuilderTab.razor.cs` and `CustomCommanderBuilderTab.razor.cs`.

---

### Mana curve — curve-aware upgrade prioritization

- [ ] Pass avg MV / sparse-CMC summary into `DeckUpgrader` gap-prioritization prompt.
      Low priority; the visual chart is the primary deliverable.

---

## Component refactoring

`DeckResults.razor` and `DeckAnalyzerPage.razor` each host several view-tab blocks whose markup
is near-identical. Extracting these into shared components follows the same pattern as
`ManaCurveChart` and makes both files easier to read and reason about.

All candidates extracted. Both host files are now thin nav-tab shells.

---

## TCGPlayer affiliate linking

A "Buy this deck on TCGplayer" action on a finished deck. Sends the full decklist into
TCGplayer's Mass Entry cart tool, tagged with an affiliate code.

See `TODO/TCGPLAYER_AFFILIATE_LINKING.md` for the full design spec.

**Prerequisites (non-code actions — must happen before commission tracking works):**
- [ ] **WotC Fan Content Policy check**: confirm the policy permits monetization for a tool
      like this before enabling any affiliate links.
- [ ] **Apply to TCGplayer's affiliate program** via Impact (impact.com).

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

## Potential upgrades

- [ ] **Opening-hand / curve simulation** — Sanity-check deck consistency by simulating
      opening hands and mana curves.
- [ ] **Multi-retailer support** — Extend affiliate linking beyond TCGPlayer (Card Kingdom, etc.)
      while keeping the builder interface slot-in-ready.
- [ ] **Split pages: Input+Calculating vs. Results** — The builder (`GuidedCommanderBuilderTab`,
      `CustomCommanderBuilderTab`) and analyzer (`DeckAnalyzerPage`) currently host input,
      live progress, and results all in one Razor component. Consider splitting each into a
      dedicated Input+Calculating page (navigates away on completion) and a Results page
      (purely presentational, driven by the saved result). This would let the Results page be
      bookmarkable/shareable and reduce component complexity. Requires deciding how in-flight
      state (progress, cancellation) transfers across a navigation boundary — one option is
      completing the build in a background service and redirecting once done.

---

## Public documentation and How-To pages

Explain to users how each page works — what their input drives, what external data is fetched,
and how the LLM steps are structured. This builds trust, helps users interpret results, and is
just genuinely interesting.

**Scope — one page per feature:**

- [ ] **Commander Builder** — the 12-stage pipeline in plain language: pool gathering
      (EDHREC + theme endpoints + Commander Spellbook near-miss combos),
      LLM classification (role assignment per card), LLM selection (ranked picks per role),
      fill engine (greedy slot-filling + reconciliation), color-fixing and repair passes.
      Explain how theme/archetype weights shift the template targets. Explain what the
      Coverage Report tab shows (overlap-aware coverage vs. physical count).
- [ ] **Commander Discovery** — how suggestions are generated (LLM `ICommanderSelector`
      using EDHREC tag-page data), what the power-level / popularity signals mean,
      how partner pairs are handled (EDHREC partner index + merged recommendation pools).
- [ ] **Deck Analyzer** — paste-in flow: classification → coverage report → bracket estimate
      (Commander Spellbook) → role gaps → Upgrade Paths → Combo Finder. Explain that
      "bracket" is Spellbook's combo-based estimate, not a manual review.
- [ ] **Upgrade Paths** — two-LLM-call pipeline: cheap model prioritises gaps by user
      feedback text, selector model proposes add+cut pairs. All suggested cards are validated
      against the EDHREC pool (whitelist check) before being shown.
- [ ] **Combo Finder** — what Commander Spellbook returns: complete combos the deck already
      enables, and near-misses (one named piece away). Explain the near-miss threshold.

**Supporting pages / sections:**

- [ ] **Data sources** — attribution page listing Scryfall, EDHREC and Commander Spellbook,
      a sentence on what each provides, and the required
      visible credit. Scryfall bulk data is CC BY 4.0; Commander Spellbook
      is MIT-licensed; EDHREC are attributed by agreement/requirement.
- [ ] **AI limitations** — the LLM classifies and selects heuristically; it can mis-classify
      cards, misread synergies, or produce a suboptimal distribution. The deck is a strong
      starting point, not a guaranteed optimal build. Encourage manual review and adjustment.
- [ ] **Glossary** — card roles (Plan, Ramp, Removal, Interaction, Wipe, Draw, Threat,
      Synergy, Payoff, Land, Unmatched), coverage vs. physical count, archetypes vs. themes,
      bracket definitions (1–5 scale), partner variants. Aimed at newer players.
- [ ] **Privacy notice** — API keys and saved results are stored only in the user's own
      browser (encrypted cookies / localStorage). Nothing is stored server-side or shared
      with third parties. State this clearly; it is also the GDPR-relevant disclosure for
      EU users.
- [ ] **Fan content disclaimer** — Magic: The Gathering card names, mana symbols, and oracle
      text are Wizards of the Coast IP. This is an unofficial fan tool, not affiliated with
      or endorsed by Wizards of the Coast. Card data is sourced via Scryfall, which operates
      under WotC's fan content and data policies. Required under the WotC Fan Content Policy.

**Legal / policy actions (non-code, owner's responsibility):**

- [ ] **EDHREC ToS review** — the current client hits EDHREC's public JSON endpoints
      (`json.edhrec.com`). Their ToS permits fan tools with attribution but prohibits
      commercial scraping. If the app grows or monetises (e.g. TCGPlayer affiliate links),
      get explicit written permission from EDHREC before proceeding.
- [ ] **WotC Fan Content Policy compliance check** — confirm the tool's use of card names
      and Scryfall data falls within the policy before adding any monetisation feature.
      (Already flagged under TCGPlayer affiliate linking — resolve that one first.)

---

## Card Data Refresh (Cache Updates)

Scryfall bulk data is cached locally with a 24-hour max age. New cards released are not available
until the app restarts and re-downloads. This creates a 24–48 hour lag.

**Options:**
- **(a) Manual refresh button** — User clicks to force re-download if stale.
- **(b) Background sync job** — Periodic task checks Scryfall's `updated_at` timestamp.
- **(c) Hybrid (recommended)** — Show "last updated X hours ago" + optional manual refresh; background job runs nightly as fallback.

**Implementation tasks (option c):**
- [ ] `ICardRefreshService` interface: checks Scryfall `updated_at`, returns whether refresh happened and when
- [ ] `CardRefreshService` impl: compares cache timestamp with Scryfall manifest, downloads if newer
- [ ] Wire into `ScryfallBulkClient` or as separate injected service
- [ ] Add "Last updated X hours ago" + "Refresh" button to Web UI
- [ ] Background job: scheduled task runs nightly

**Scope:** ~2–3 hours for manual refresh (option a); add another 2–3 hours for background job (option b).

---

## Summary of completed work

All four projects compile; 436 tests pass. All items below are complete.

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

**Tests:** 436 tests (Core rules, archetypes, templates, budget, discovery, partnership index, selection, fill engine, color fixing, repair, BYOK, integration, deck analyzer, upgrade parser, combo pool source, creature type catalog pluralisation and slug matching). All green. Web project exposes internals to the test project via `InternalsVisibleTo`.

**Component refactoring:** Display helpers (`SecondaryBadge`, `BadgeInfo`, `BracketTagLabel`, `BracketTagCss`) centralised in `CardRoleDisplay`. `DeckBuildStages` constant extracted to `BuildRequestFactory`. Shared components extracted to `Components/Shared/`: `UpgradePathsPanel`, `CombosPanel`, `CardsByTypeList` (with `IsLocked` added to `AnalyzedCard`), `LockedCardInput` (with `LockedCardState` record), and `RoleTargetEditor` (replaces hand-crafted `RenderFragment` builder API code). `CoverageReportPanel` (in `Components/Results/`) owns the full Coverage Report tab — summary table, role buckets, basic lands, runner-ups — parameterised with `RenderFragment` slots (`SummaryHeaderActions`, `SummaryBodyPrefix`, `TargetCellContent`, `Alerts`) for host-specific customisation. Both `DeckResults` and `DeckAnalyzerPage` are now thin nav-tab shells.

**Deployment:** Hetzner CX23 VPS + Docker Compose + Caddy reverse proxy (automatic TLS). GitHub Actions CI/CD: test → build → push to `ghcr.io/jacob-winther-solutions/edh-deck-builder` → SSH deploy. Live at https://deckconsult.winther-solutions.dk. Full setup in `TODO/DEPLOYMENT.md`.

**EDHREC theme-specific tag endpoints:** `IEdhrecClient.GetCommanderThemePageAsync` (`/commanders/{slug}/{themeSlug}.json`) and `GetTagsPageAsync` (`/tags/{themeSlug}.json`) implemented. `SuggestionSource.GetCommanderThemeRecommendationsAsync` and `GetTagsAsync` map pages to `CardCandidate` lists. `DeckBuilder.GetThemePoolAsync` fires all `(commander × theme)` and per-theme tags-page requests in parallel, merges into `edhrecPool` before classification.

**Deck Strategy description:** `DeckAnalyzer` collects Plan-primary cards after classification and makes a single lightweight LLM call (`describe_plan` forced tool, Haiku/lite model, 512 tokens) to produce a 2–3 sentence natural-language description of the deck's win condition. Shown as a "Deck Strategy" callout at the top of the Coverage Report tab and included in exported analysis markdown. Gemini path supported via `GeminiSchemas.BuildPlanDescriptionSchema`.
