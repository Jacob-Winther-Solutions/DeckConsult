# Data Sources

Documents the sanctioned card data sources in use and the sources that must not be wired.

---

## Implemented sources

### Scryfall — card facts + legality

Scryfall bulk data (JSONL.gz) is the sole authority on card facts and legality. Color identity
and legality are never recomputed from oracle text — they are stored as Scryfall reports them.
Bulk data is CC BY 4.0; attribution is required wherever card data is surfaced.

- Legality keys used: `commander` (current). `historicbrawl` is available on the legalities
  object and would be read for a future Historic Brawl format profile.
- Bulk endpoint: `https://api.scryfall.com/bulk-data` (type: `oracle_cards`).
- Creature type catalog: `https://api.scryfall.com/catalog/creature-types`.

### EDHREC — commander recommendations

EDHREC JSON endpoints (`json.edhrec.com`) provide commander-specific card recommendations,
theme-specific card lists, partner indexes, and popular theme tags.

- Used for: pool gathering per commander, theme tag pools, popular theme badges, partner pair
  data, and Commander Discovery suggestions.
- EDHREC are attributed by agreement/requirement. Their ToS permits fan tools with attribution
  but prohibits commercial scraping. Get explicit written permission before adding any
  monetisation feature.

### Commander Spellbook — combos + bracket estimation

Open source (MIT), hosted at `https://backend.commanderspellbook.com`.

- `POST /find-my-combos` — given a card list, returns complete combos the deck enables and
  near-misses (one named piece away from a complete combo).
- `POST /estimate-bracket` — combo-based bracket classification (1–5 scale).
- Used for: Combo Finder tab, bracket estimate in Deck Analyzer, near-miss combo pieces injected
  into the builder pool via `ComboPoolSource`.

---

## Excluded sources — must NOT be wired

| Source         | Reason                                                                       |
| -------------- | ---------------------------------------------------------------------------- |
| Moxfield       | ToS prohibits scraping; no official public API; access is permission-gated.  |
| Archidekt      | Unofficial/undocumented API, goodwill-dependent, may change without notice.  |
| mtgdecks.net   | Bot-blocked; no sanctioned API; data duplicates EDHREC.                      |
| AetherHub      | No sanctioned data API; scrape-only.                                         |
| MTGGoldfish    | Explicit "no reproduction without consent" terms.                            |
| Untapped.gg    | Commercial tracker; no open/free public API.                                 |
| TopDeck.gg     | Decklists are rarely uploaded: a 90-day lookback with many tournaments found zero published decklists. No usable card data. |
| MTGJSON        | Overlaps Scryfall; only relevant if offline bulk or precon data becomes a goal. |

If a real need for any excluded source emerges later, it requires a permission or collaboration
step — not a direct integration.
