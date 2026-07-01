# Feature: Extend Data Sources to the Full Sanctioned Set

## Purpose

Extend the `Infrastructure` layer from its current two clients (Scryfall, EDHREC)
to the full set of **sanctioned** MTG data sources, so the builder can support
Commander today and branch cleanly into Historic Brawl and Duel Commander next.
Concretely this adds two new clients (**Commander Spellbook**, **TopDeck.gg**),
optionally a third (**MTGJSON**), and extends the existing EDHREC client to Brawl.

---

## How to read this document

- **Verified facts** — URLs, endpoints, auth, rate limits, and policy constraints —
  were confirmed against live docs/responses and are marked accordingly. Treat these
  as authoritative.
- **Illustrative code** (interfaces, request shapes) is a sketch only. **Adapt all
  type names, namespaces, SDK/HTTP-client patterns, and DI registration to the real
  codebase.** Do not treat the sketches as literal.
- This is an *implementation* spec for settled decisions. It is **not** an invitation
  to redesign the architecture below.

---

## Guardrails (settled — do not redesign)

1. **Scryfall is the sole authority on card facts and legality.** Never recompute
   color identity or legality from oracle text. This already holds; it must hold for
   the new sources too — card names returned by any other source resolve back through
   Scryfall.
2. **EDHREC is for recommendations, not card truth.**
3. **Inward-only dependencies.** Each source is an `Infrastructure` client implementing
   a Core-defined interface. Core stays dependency-free. The Agent/Web layers do not
   change shape; these sources feed the existing deterministic scoring/discovery layer.
4. **The LLM seam is unchanged.** New data feeds deterministic code (weights, scoring,
   fill), never free-running tool loops.
5. **Sanctioned-only.** Only add the sources in this doc. The "Excluded sources" list
   below must **not** be wired, even where they would fill a gap.

---

## Sources

### 1. Scryfall — existing (card facts + legality)

No new client work. Already covers the branch formats: legality keys
`commander`, `historicbrawl`, `brawl`, `duel`, `oathbreaker`, `paupercommander`
exist on the Scryfall legalities object. Ensure the format-legality check reads the
correct key per active format rather than assuming Commander.

### 2. EDHREC — existing, extend to Brawl (recommendations)

EDHREC already publishes Historic Brawl recommendation pages under its JSON endpoints
(`json.edhrec.com`). Extend the existing client to resolve the Brawl path variants in
addition to Commander. **Note:** EDHREC does *not* model Duel Commander's distinct
banlist/metagame — do not use EDHREC as the DC recommendation source.

### 3. Commander Spellbook — NEW (combos + bracket signal)

Serves the **Combo** archetype, the **Synergy/Plan** roles, and the **five-bracket
system**. Applies to all your singleton formats (Commander, Brawl, Duel Commander).

**Verified facts**
- Base: `https://backend.commanderspellbook.com`
- Endpoints: `find-my-combos` (POST a card list → combos present + one-card-away),
  and `estimate-bracket` (combo-based bracket-relevant classification).
- Open source, **MIT-licensed** (`SpaceCowMedia/commander-spellbook-backend`).
- This is the same combo engine that **already powers EDHREC's combo feature**, so
  it is consistent with data you already surface.
- Combos carry color identity (Commander rules), EDHREC-derived popularity, and a
  bracket bucket. Their bracket buckets map to the 1–5 scale roughly as:
  Casual→1, Precon Appropriate/Oddball→2, Powerful/Spicy→3, Ruthless→4. Confirm the
  current mapping against their source before relying on exact numbers.

**Feeds:** Combo detection for a candidate deck; bracket estimation input.

### 4. TopDeck.gg — NEW (competitive metagame, incl. Duel Commander)

The competitive "what wins in tournaments" signal, and the cleanest sanctioned path
to **Duel Commander** — same API, different format string.

**Verified facts**
- Base: `https://topdeck.gg/api` · primary endpoint: `POST /v2/tournaments`
- OpenAPI spec: `https://topdeck.gg/openapi.json`
- Auth: `Authorization: <API_KEY>` header (raw key, free from the account page).
- Rate limit: **100 requests/min** on most endpoints (lower for bulk); `429` on
  exceed with a `Retry-After` header.
- **Attribution is mandatory:** any project using the API must show a visible credit
  and a link back to TopDeck.gg. This must appear in the Web UI wherever this data is
  surfaced.
- Format strings (case-sensitive), game `"Magic: The Gathering"`: `"EDH"` for
  Commander, `"Duel Commander"` for DC. **Brawl is NOT supported** (Arena format, not
  tournament-organizer data).
- Query mode filters: `format`, `start`/`end`/`last` (days back), `participantMin/Max`,
  `columns`, `rounds`. Standings can include `decklist` (text, using `~~Commanders~~`
  section headers) and `deckObj` (`{ "Commanders": {…}, "Mainboard": {…} }`) **when
  structured deck data is available**.
- Decklists are returned **only** when a tournament has ended or the organizer enabled
  "Show Decks." Coverage is therefore partial — handle missing lists gracefully.

**Feeds:** A competitive-frequency signal, tagged as the high-bracket population, kept
distinct from EDHREC's broader population.

**Implementation shape:** Because of volume + rate limits, this is a **periodic ingest
job**, not a live per-build call. Pull EDH/DC tournaments over a window, extract
`deckObj` per standing, and aggregate into a local "frequency by commander" table the
Agent consults. Prefer `deckObj`; fall back to parsing the `~~Section~~` text list.

### 5. MTGJSON — OPTIONAL (offline bulk / precon data)

Add only if you want offline bulk or preconstructed decklists. Overlaps Scryfall on
legality, so it is not required for the branch formats.

**Verified facts**
- Free, rebuilt daily; downloadable as JSON / CSV / SQLite / Parquet (e.g.
  `AllPrintings`, `AllDecks`). File server under `mtgjson.com` (confirm the current
  `api/v5` path before wiring).
- Legalities model explicitly includes `historicBrawl`, `standardBrawl`, `oathbreaker`,
  `pauperCommander`.
- Bakes in per-card `edhrecRank` and `edhrecSaltiness`, plus preconstructed `Deck`
  objects with a `commander` field.
- GraphQL access is a Patreon-only perk; the file downloads are free.

**Feeds:** Optional offline card store; precon seed lists.

---

## Format coverage matrix

| Need                      | Commander            | Historic Brawl        | Duel Commander        |
| ------------------------- | -------------------- | --------------------- | --------------------- |
| Card facts + legality     | Scryfall             | Scryfall              | Scryfall              |
| Recommendations           | EDHREC               | EDHREC (Brawl paths)  | *(gap — see below)*   |
| Combos + bracket          | Commander Spellbook  | Commander Spellbook   | Commander Spellbook   |
| Competitive popularity    | TopDeck.gg / EDHREC  | **gap** (no open src) | TopDeck.gg            |

---

## Known gaps (do not paper over with excluded sources)

- **Historic Brawl competitive popularity.** No sanctioned open source. The real signal
  lives with Untapped.gg (commercial, no open/free public API). Accept EDHREC +
  Scryfall for Brawl recs/legality and leave popularity as a `TODO` — do **not** scrape
  an aggregator to fill it.
- **Duel Commander recommendations.** TopDeck.gg gives competitive DC results, but
  there is no EDHREC-equivalent recommendation feed tuned to the DC banlist. Derive DC
  recommendations from aggregated TopDeck.gg data, or leave as a `TODO`.

---

## Excluded sources — must NOT be wired

| Source                          | Reason                                                        |
| ------------------------------- | ------------------------------------------------------------ |
| Moxfield                        | ToS prohibits scraping; no official public API; access is permission-gated (custom User-Agent by request). |
| Archidekt                       | Unofficial/undocumented API, goodwill-dependent, may change. |
| mtgdecks.net                    | Bot-blocked; no sanctioned API; data duplicates EDHREC.      |
| AetherHub                       | No sanctioned data API; scrape-only.                         |
| MTGGoldfish                     | Explicit "no reproduction without consent" terms.            |
| Untapped.gg                     | Commercial tracker; no open/free public API.                 |

If a real need for any of these emerges later, it goes through a permission/collaboration
step first — not into this feature.

---

## Cross-cutting implementation notes

- **One interface per source**, defined in Core, implemented in Infrastructure; register
  via DI. Keep each client's failure isolated (timeouts, cancellation, retry/backoff on
  `429`).
- **Caching / ingest cadence:** Scryfall/EDHREC/Commander Spellbook can be
  request-time-with-cache; TopDeck.gg is a scheduled ingest into a local aggregate. MTGJSON
  (if used) is a periodic bulk refresh.
- **Attribution obligations** are binding and must surface in the Web UI: TopDeck.gg
  (visible credit + link), Commander Spellbook (MIT notice), MTGJSON (acknowledgment if
  adopted).
- **Card identity always resolves through Scryfall** — names from `deckObj`, combos, or
  MTGJSON are looked up against Scryfall for authoritative facts/color identity.

---

## Open decisions (owner's call — do not decide unilaterally)

1. **TopDeck.gg aggregation weighting:** raw card frequency vs. win-rate/standing-weighted
   (cards in top-finishing decks count more). This determines whether the aggregate table
   stores standings alongside counts.
2. **Adopt MTGJSON now or defer.** Optional; only pulls its weight if offline/precon is
   wanted.
3. **How (or whether) to close the Historic Brawl popularity gap** before public launch.

---

## Illustrative only — adapt to the real codebase

```csharp
// Sketch. Names/namespaces/return types are placeholders.
public interface IComboSource            // Commander Spellbook
{
    Task<ComboResult> FindCombosAsync(IReadOnlyList<string> cardNames, CancellationToken ct);
    Task<BracketEstimate> EstimateBracketAsync(IReadOnlyList<string> cardNames, CancellationToken ct);
}

public interface ICompetitiveMetaSource  // TopDeck.gg (via local aggregate)
{
    Task<CommanderFrequency> GetFrequencyAsync(string format, string commander, CancellationToken ct);
}
```

```jsonc
// Illustrative TopDeck.gg request body (query mode)
{
  "game": "Magic: The Gathering",
  "format": "Duel Commander",   // or "EDH"
  "last": 90,
  "participantMin": 16,
  "columns": ["name", "decklist", "wins", "draws", "losses"]
}
```
