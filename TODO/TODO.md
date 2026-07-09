# TODO — EdhDeckBuilder

A living list of deferred work. Check items off as they land.

---

## Multi-Provider LLM Support — Gemini LIVE (2026-07-08)

Anthropic and Google Gemini are both fully wired end-to-end. The Gemini adapters use a custom
REST client (not the OpenAI-compatible SDK path) posting directly to `generateContent` with a
structured `responseSchema`. See `README.md` and `CLAUDE.md` for the architecture; the archived
`TODO/Archive/GEMINI_IMPLEMENTATION_NOTES.md` records the original blocked state for context.

**Still deferred:** GitHub Models LLM adapters — the Azure.AI.Inference SDK type surface is still
in beta. UI, cookie, and key storage for GitHub Models remain in place; when the SDK stabilizes,
the three adapters can slot in following the same `IUsageTrackerAware` + factory pattern the
Anthropic and Gemini adapters use.

---

---

## Bugs to Investigate

### LLM Classifier returns all Unmatched on cold cache (2026-07-07)

**Observed:** On first build after clearing the classification cache, the LLM classifier returns
**all 257 cards as Unmatched**, resulting in 0 cards being filled. On the second identical build,
the cache is warm and the classifier returns proper role assignments (56 valid roles, 201 Unmatched).

**Evidence:** Test logs show:
```
Run 1 (empty cache):  Unmatched: 257, FillEngine: 0 cards committed, Final: 35 cards
Run 2 (warm cache):   Unmatched: 201, FillEngine: 47 cards committed, Final: 82 cards
```

**Root cause hypothesis:** The LLM (Haiku) is consistently failing to parse or return valid role
strings on the first batch when there's no prior cache context. Possible causes:
- Tool schema not being honored (all cards falling through to default `Unmatched`)
- LLM defaulting to unparseable role names (misspellings, inconsistent casing despite `ignoreCase`)
- Prompt context is missing on cold start (system prompt not cached yet)
- Response format breaking on large batches (~65 cards per batch)

**Impact:** Users who clear cache or start fresh get a broken deck build. Workaround: rebuild immediately (uses cached responses).

**Next steps:**
1. Add debug logging to `LlmClassifier.ParseRole()` to log each role string before/after parsing
2. Log the raw tool response JSON to see what role strings are actually coming back
3. Check if prompt caching (once SDK exposes `SystemBlockParam` API) fixes this
4. Consider warming the classifier cache during app startup with a small batch

**Blocked on:** Investigation — need to reproduce and inspect raw LLM responses.

---

### Partner pair data mismatches in EDHREC (2026-07-08)

**Observed:** During CardRepository initialization, debug logs show `dbug: ... Partner pair not found in card repository` for ~10 EDHREC partner pairs. Example: `Alisaie Leveilleur // Alphinaud Leveilleur + Amy Pond // Rory Williams`.

**Context:** `PartnershipIndexBuilder.BuildFromEdhrec()` attempts to resolve each EDHREC partner combo by card name. If either card is not found in the Scryfall index (by name match), the combo is silently dropped and logged as debug.

**Root cause candidates:**
- EDHREC uses card names that don't exactly match Scryfall (e.g. "Alisaie" vs. a full front-face name)
- EDHREC lists double-faced cards by a specific face, Scryfall stores them differently
- EDHREC partner list is stale relative to Scryfall (rare)
- EDHREC cards use Scryfall IDs but the index-builder is name-based (brittle seam)

**Impact:** Low — affected pairs don't reach the partnership index, so they won't appear as suggestions in Discovery. If a user manually enters them as commanders, the builder falls back to merged single-commander pools (graceful).

**Next steps:**
1. Audit the ~10 missing pairs: check if they exist in Scryfall by exact name, partial name, or ID
2. If name-matching is the issue, consider migrating `PartnershipIndexBuilder` to ID-based lookup (EDHREC provides IDs; infrastructure already supports `GetByScryfallIdAsync`)
3. Confirm whether the affected pairs are valid EDH partnerships (legal, indexed on EDHREC for a reason)
4. If confirmed valid, update the index builder; if archival/errata, update EDHREC data source comment

**Blocked on:** Investigation — need to manually verify affected partner pairs against Scryfall.

---

## Commander Discovery — progress step display

The Deck Builder page streams its 10-stage pipeline progress to the user as it runs (stage names
+ spinner). The Commander Discovery page does not — it shows a static spinner with no indication
of which step is running. Parity task: wire up the same progress-tracking pattern on the
Discovery page so users can see the individual steps (e.g. "Fetching EDHREC pool…", "Ranking
candidates…", "Building suggestions…") as they complete.

**Implementation tasks:**

- [ ] Define a discovery progress model (stage name + optional detail, e.g. candidate count)
      mirroring the `BuildProgress` type used by the Deck Builder pipeline.
- [ ] Instrument `CommanderDiscovery` to emit step events via a callback or `IProgress<T>`.
- [ ] Update `DiscoveryPage.razor` / `DiscoveryPage.razor.cs` to subscribe to those events and
      render them in a step list (spinner per step, checkmark when done).

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
      resolvedLimit)` in `GuidedTab.razor.cs` and `CustomTab.razor.cs`. The two call sites are the
      only places that need updating.

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

## Potential upgrades

Features and enhancements worth considering for future iterations:

- [ ] **Opening-hand / curve simulation** — Sanity-check deck consistency by simulating
      opening hands and mana curves.
- [ ] **Plain text decklist export** — For users who prefer to manually paste into external
      deck builders (TCGPlayer Mass Entry, etc.). Shares the `CartLine` mapping with the buy button.
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

All four projects compile; 328 tests pass.

**Core & Infrastructure:** Domain model, rules, templates, archetypes, themes, bracket system. Scryfall bulk client, EDHREC client (single commander + partner pairs), `SuggestionSource` merge. `CanBeCommander` extended for planeswalker-commanders. MDFC/DFC back-face data fully supported. Colorless basic land (Wastes).

**Agent pipeline:** `LlmClassifier` (Haiku, forced tool call, batched, cached except Plan/Synergy/Payoff), `LlmSelector` (user model, forced tool call, per-build rationale). `FillEngine` (greedy + reconciliation, max 50 iterations), `ColorFixingPass` (pip-demand scoring). Deterministic `RepairEngine` + `DeckBuilder` (12-stage pipeline). Multi-role classification: role profiles, secondary contributions, coverage accounting.

**Multi-provider LLM (Anthropic + Gemini):** Three provider-agnostic adapters (`LlmClassifier`, `LlmSelector`, `LlmCommanderSelector`) dispatch to one of two `ILlmClient` implementations via `ILlmClientFactory`. Anthropic path via `ClaudeHttpLlmClient` — direct HTTP to `api.anthropic.com/v1/messages`, no Anthropic C# SDK. Gemini path via `GeminiHttpLlmClient` wrapping `GeminiRestClient` — posts to `generateContent` with `responseSchema` structured output. `GeminiSchemas` translates our schema shape to Gemini's OpenAPI 3.0 subset (uppercase types, `propertyOrdering`, `format: enum`). `GeminiRateLimiter` (Scoped) enforces per-circuit RPM pacing. `GeminiRestClient` retries 429/502/503/504 with `Retry-After`-aware backoff; parses Google's structured error body so free-tier `limit: 0` gating is diagnosable. MAX_TOKENS handled distinct from JSON parse errors. Model picker covers `2.5 Flash`, `2.5 Flash Lite`, `3.1 Flash Lite` (default — 500 RPD), `3 Flash`, `3.5 Flash`, `2.0 Flash*` (needs billing).

**Cost accounting:** `Instrumentation/ModelPricing.cs` — per-model USD rates per 1M tokens for both providers. `UsageTracker` uses it for per-call rows and summary totals; mixed-provider runs tally correctly. Free-tier Gemini calls show the "what you'd pay on paid tier" estimate. `IUsageTrackerAware` marker interface removes type-specific dispatch — all three adapters implement it, `DeckBuilder` and `CommanderDiscovery` wire the tracker through it.

**BYOK & authentication:** `SessionApiKeyProvider` (scoped per-circuit) with triple-key storage (Anthropic / GitHub / Google). `ClaudeHttpLlmClientFactory` (Anthropic HTTP seam), `GeminiLlmClientFactory` (Gemini REST client seam), both implementing `ILlmClientFactory`. `KeyTester` (1-token probe for Anthropic; format check for Google/GitHub). 401/403 → `ApiKeyRejectedException` wrapping on both provider paths. Data Protection-encrypted cookies (30-day expiry) for each provider plus a selected-model cookie; cookies win over `Provider:Default` in appsettings. Prompt caching implemented on the Anthropic path (`cache_control` on last tool definition); Gemini path ignores `EnableCaching`.

**Web UI:** Commander search + deck builder. Three deck views (by role / by type / all cards). Coverage summary, runner-up panel, cut suggestions. Budget input & enforcement (per-card + total). Archetype/theme picker with weight sliders; 29 themes + custom escape hatch. Bracket selection. Export build report (`.md` download). Color identity picker with exact-match option. Provider toggle (Anthropic / GitHub Models / Google AI Studio) with per-provider help text and dynamic model dropdown.

**Commander Discovery:** Standalone `/discover` page with two tabs: Guided (LLM-assisted, archetype/theme-driven) and Custom (free-text strategy description). Ranked commander suggestions with art, rationale, and power level. Contiguous rank normalization in `BuildSuggestionsFromResults` — display is always 1..N regardless of what the model emits. Partner-pair support (all 8 variants: Partner, Partner with, Background, Friends Forever, Doctor's Companion, Survivors, Character Select, Father & Son). EDHREC partner index integrated. Deck builder pool gathering queries partner-pair endpoints with redirect handling and canonical caching. Graceful fallback to merged single-commander pools.

**Deck results & logging:** Saved locally (3-max, Data Protection cookie). Token usage logging with per-call table + summary total across all discovery and build flows, per-provider pricing.

**Deck Download:** Markdown export with header, role buckets (with rationale), runner-ups, coverage summary, raw decklist (ready to paste).

**Tests:** 328 tests (Core rules, archetypes, templates, budget, discovery, partnership index, selection, fill engine, color fixing, repair, BYOK, integration). All green.
