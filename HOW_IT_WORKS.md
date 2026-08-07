# How EdhDeckBuilder Works

A plain-language explanation of what this tool does for you, what you need to provide,
and where your judgment still matters.

---

## Commander Discovery

If you know what kind of deck you want to build but haven't settled on a commander yet, use
the **Commander Discovery** page (`/discover`). You describe your strategy, and the tool suggests
commanders that fit.

### Guided discovery

The **Guided** tab asks for:

- **Archetype(s)** — how you want to win (Control, Aggro, Combo, Midrange)
- **Theme(s)** — what mechanical identity you want (Big Mana, Aristocrats, Voltron, etc.)
- **Color identity (optional)** — filter the candidate pool to specific colors
- **Bracket (optional)** — filter by power level (Casual through cEDH)
- **Budget (optional)** — filter by price point (per-card or total deck budget)
- **Additional notes (optional)** — free-text description of your strategy

The tool queries all legal commanders, scores them against your inputs using the LLM you've
connected (Claude or Gemini), and shows you a ranked shortlist with explanations. Click any
result to jump to the builder with that commander pre-selected.

### Custom discovery

The **Custom** tab is for when you want to describe your deck idea in prose:

- **Deck description (required)** — a free-text explanation of the strategy, play style, and
  win conditions you have in mind. Be as specific as you'd like — the more detail you provide,
  the better the suggestions.
- **Color identity (optional)** — filter the commander pool by color
- **Bracket (optional)** — power level guidance
- **Budget (optional)** — price constraints

The tool uses your description to find fitting commanders without requiring you to break down
archetypes and themes upfront. This is useful if you have a specific play pattern in mind that
doesn't neatly fit preset categories.

Both tabs show results as a ranked list of suggested commanders. Each suggestion includes the
commander's image, ability text, and a written explanation of why it fits your criteria.

Each result also shows **popular EDHREC themes** for that commander — the top 6 themes from
EDHREC's tag data, ranked by how many decks on EDHREC use them. Themes already tracked in the
app (archetypes like Aggro/Control, mechanical themes like Aristocrats or Voltron, and creature
type tribes) are highlighted in blue; unknown EDHREC-specific tags are shown in grey.

Click any suggestion to jump to the builder with that commander pre-selected. If you have not
chosen a theme yourself, the builder link automatically presets the top popular EDHREC theme that
maps to one of the app's known themes — giving you a reasonable starting point without requiring
you to pick one manually.

---

## What you provide

### Before you start

**An API key from one of the supported LLM providers.** The builder runs on your own key; you
pick which provider to use.

**Anthropic Claude** — Create a key at
[console.anthropic.com](https://console.anthropic.com/settings/keys) (free to create, pay per
token used). Once connected, pick which model is used for card selection: Haiku is the default
(fast and cheap); Sonnet and Opus give better rationale quality at higher cost. Classification
always uses Haiku regardless — it's the cheap batch step.

**OpenAI** — Create a key at [platform.openai.com/api-keys](https://platform.openai.com/api-keys).
Requires a paid account with billing credit attached (no free tier for the API). Classification
always uses `gpt-4o-mini`; you can select `gpt-4o`, `o4-mini`, or `o3` for card selection.
Note: the o-series reasoning models (`o4-mini`, `o3`) are significantly more expensive and slower
than the GPT-4o family — use them if rationale quality is the priority.

**Google Gemini** — Create a key at
[aistudio.google.com/apikey](https://aistudio.google.com/apikey) (free tier available). Gemini's
free tier has meaningful daily limits — as low as 20 requests per day on `Gemini 2.5 Flash` —
so it's best suited for occasional builds or iteration on a lite model. The default is
**Gemini 3.1 Flash Lite** because its free-tier daily budget (500 requests) is roughly 25×
higher than the standard models', making it much better for testing and repeated builds.
Heads-up: some models (notably `Gemini 2.0 Flash` and `2.0 Flash Lite`) only allocate free-tier
quota when a billing account is attached to the Google Cloud project — even at $0 usage. If you
see an error mentioning `limit: 0`, that's the cause. Attaching billing unlocks the free tier
for those models; you won't be charged if you stay within the allowance.

Paste your chosen provider's key into the "Connect your API key" card at the top of the builder.
Check "Remember my key" and it will be saved as an encrypted cookie for 30 days so you only need
to do this once. You can disconnect at any time.

### Required

**A commander (or a partner pair).** This is the single most important input — it determines
the color identity the entire 99 must respect, and it shapes how the LLM classifies every card
in the candidate pool. The tool can handle one commander, or two commanders for a partner /
background pairing (which reduces the 99 to 98 remaining slots).

**At least one archetype with a weight.** Archetypes describe *how* your deck wants to win —
the strategic posture and power-level of the game plan:

| Archetype | What it means |
|---|---|
| Midrange | Resilient threats with enough interaction to pivot between roles |
| Control | Win late through card advantage and dense interaction |
| Aggro | Low curve, lots of threats, end the game before it goes long |
| Combo | Assemble and protect a game-ending card interaction |

You can mix archetypes by assigning weights to each. `Midrange: 0.6, Combo: 0.4` means a
midrange deck with a combo finish; `Combo: 1.0` means pure combo. The tool normalizes weights
internally. You must provide at least one; the blend determines how role targets (Tutor count,
disruption density, land count, etc.) are shifted from the neutral baseline.

**At least one theme.** Themes describe *what* your deck does mechanically — the specific
card types and synergies it is built around. They answer a different question from archetypes
and are independent from them:

| Theme | What it does |
|---|---|
| Big Mana | Ramp hard, then overpower the table with expensive spells |
| Aristocrats | Sacrifice synergies and incremental drain; values recursion heavily |
| Voltron | Suit up a single threat (often the commander) and protect it |
| Tokens | Go wide with creature tokens and anthems or payoffs that scale on count |
| Lifegain | Convert life total padding into card advantage and board presence |
| Reanimator | Fill the graveyard cheaply, then cheat large threats back into play |

**You need to provide both because they answer different questions.** An archetype alone does
not tell the builder which card types to favor — Combo just means "tutor for your plan and
protect it," but it does not know whether that plan is an Aristocrats sacrifice loop or a
Voltron commander damage kill. Conversely, a theme alone does not set the strategic posture —
an Aristocrats deck could be Midrange (grind value), Aggro (fast drain), or Combo (instant-win
loop). The archetype determines *density* (how much disruption, how high the curve); the theme
determines *identity* (what your cards are actually doing). Together they fully specify the
build direction.

Weights on themes work the same way as on archetypes: `Tokens: 0.7, Aristocrats: 0.3` means
a primarily token deck that also sacrifices them for value.

The **Tribal** theme has an additional creature type field. As you type, a dropdown suggests
matching creature types (sourced from Scryfall's creature type catalog) to help with spelling
and singular-vs-plural form — for example, "Elv" completes to "Elves". Custom tribe names are
also accepted if the type doesn't appear in the list.

### Optional

**Power bracket (1–5).** Corresponds to the Commander Rules Committee's bracket system:

| Bracket | Description |
|---|---|
| 1 | Casual / jank |
| 2 | Low-power |
| 3 | Medium — the common default |
| 4 | High-power |
| 5 | Cedh (Competitive EDH) |

The bracket shifts what kinds of cards the selector considers appropriate. Bracket 1 avoids
tutors; Bracket 5 expects fast mana and optimized interaction. This is treated as a soft
guideline to the LLM, not a hard filter.

**Budget.** Two independent budget controls, both optional:

- **Per-card maximum** — no single card in the 99 will exceed this price (e.g. "$5 per card").
  The most direct way to avoid expensive staples like Mana Crypt or original dual lands.
- **Total deck budget** — the sum of all 99 cards stays within this amount (e.g. "$150 total").
  Useful when you are fine with one or two pricier pieces but want to control overall spend.

Both are soft preferences, not hard cutoffs — if no affordable card can fill a role, the tool
picks the best available option and flags it so you know what to swap. Cards that breach a
budget limit are highlighted in the result, and the runner-up list prioritizes affordable
alternatives.

**Soft constraints.** Free-text curve guidance ("strongly favor low mana-value cards") or
additional hints forwarded to the card-selection prompt. Useful for unusual strategies the
archetypes and/or themes don't fully capture.

**Must-Include Cards.** A list of cards that must appear in the finished deck, entered one
per line in the "Must-Include Cards" textarea. These are treated as locked slots — they are
committed to the deck before the fill pass runs and are completely protected from removal:

- The tool validates each name before the build starts. Unrecognized names block the build
  entirely; names that are wrong for the commander's color identity generate a warning (not a
  hard block, in case you are intentionally testing a configuration).
- Locked cards are **excluded from all budget enforcement**. A card you already own that costs
  more than your per-card or total budget limit is kept regardless — budget only applies to
  cards the tool selects.
- Locked cards are never candidates for cut suggestions. The cut list only includes cards the
  tool chose on its own.
- In the result views, locked cards show a 🔒 badge so they are easy to identify.
- If the number of locked cards fills all available slots (99 minus the commander count), the
  build button is disabled — there is nothing left for the tool to select.

---

## What the tool decides on its own

Once you've provided the inputs above, the following happen automatically — you do not
need to intervene:

**Template resolution.** Your archetypes, themes, and bracket are blended into a single
set of per-role coverage targets (how many Ramp cards, how much Disruption, etc.). This is
purely deterministic math — no LLM involved.

**Pool gathering.** Recommendations from different sources are fetched for your commander(s) 
and merged into a candidate pool. Cards that are illegal (wrong color identity, banned) are 
filtered out before the build begins.

**Card classification.** Every card in the pool is given a primary role (Ramp, Card Advantage,
Targeted Disruption, etc.) and optional secondary roles (e.g. a card that both ramps and draws
cards is classified as Ramp with a secondary Card Advantage contribution). This is the first
LLM call — a fast, cheap batch classification. Results are cached across builds so most cards
only need to be classified once.

**Card selection.** The tool fills each role bucket by asking the LLM to rank the classified
candidates for this specific commander and strategy, then taking the top-ranked cards until the
coverage target is met. Each selected card gets a written rationale — *why* it belongs in this
deck. This is the second LLM call.

After the build, a **token usage summary** is written to the console with per-call token counts
and an estimated USD cost. The cost is the paid-tier estimate for the model you used — free-tier
Gemini calls still show a number so you can see what usage would cost outside the free tier.

**Color-fixing.** After the spell slots are filled, the tool upgrades some basic lands to
non-basic color-fixing lands from the pool (dual lands, fetch lands, etc.), ranked by how much
mana fixing they provide for the colors this deck actually needs. It never drops below a set 
number of basic lands and never makes non-basics more than a set percentage of the land base.

**Basic land distribution.** Remaining basic land slots are distributed across the colors in
the commander's identity, proportional to how many cards in the 99 actually need each color.
A heavily green ramp deck gets more Forests than Mountains even if it runs both colors.

**Repair.** If any committed card turns out to violate color identity (an edge case that can
happen with complex source data), it is swapped out deterministically for the best legal
alternative available in the pool.

---

## What you get back

When the build finishes you are taken to a dedicated results page (`/results/{id}`). The
page is saved to your browser's local storage, so it survives a page reload and can be
revisited later — the tool keeps your three most recent builds and automatically removes
the oldest one when a new build is saved.

**The 99 non-commander cards**, split into two parts:
- **Deck** — every non-basic card (spells, utility lands, MDFCs), each with a role, a rank
  within that role, and the LLM's written rationale for why it's here.
- **Basic land counts** — how many of each basic land type (e.g. "20× Forest, 11× Mountain").

**Runner-ups** — the top 30-ish cards that were evaluated but not selected. Lets you swap in
alternatives you prefer without triggering a full rebuild.

**Coverage diagnostics** — a comparison of the planned role targets vs. what was actually
achieved. If a role is short (thin pool for this commander) or over-served, it's flagged.

**Cut suggestions** — if any role is over-covered, the tool surfaces the weakest cards in
that role so you know what to cut first if you want to tighten the list.

**Export Build Report** — the "Export Build Report" button downloads the full result as a
self-contained `.md` file. It contains the build metadata (commander, archetype/theme
weights, bracket, budget, date), every card with its rationale organized by role, the
coverage summary, the runner-up list, and a plain `1 Card Name` raw decklist at the bottom
ready to paste into Moxfield, Archidekt, or any other deck builder. A separate "Copy
Decklist" button copies just the raw card list to the clipboard for quick import.

---

## What you still decide

**The final deck is a starting point, not a finished product.** The tool's outputs are designed
to get you to a coherent, legal, well-structured 99 — not to replace the judgment calls only
you can make:

- **Is this commander the right choice?** The tool builds the best deck it can for whoever
  you give it, but picking a commander that fits your playstyle is your call.
- **Final card choices.** The tool makes informed picks but reasonable people disagree. If a
  card the tool chose feels wrong for your table, swap it with a runner-up — that list exists
  precisely for this.
- **Table politics and personal preference.** Cards that are technically correct but feel bad
  at your specific table — stax pieces, land destruction, infinite combos — are not filtered
  out unless your bracket choice discourages them. You decide what's appropriate.
- **Manual swaps.** The runner-up list is there precisely because reasonable people disagree
  on card choices. If you have a personal staple you always run, swap it in for the weakest
  card in that role.
- **Iteration.** A second run with different archetype weights or a different bracket will
  produce a meaningfully different list. Treat the first output as a draft.

---

## Deck Analyzer

If you already have a deck, use the **Deck Analyzer** page (`/analyze`) to evaluate it and get
targeted upgrade suggestions.

### What you paste in

**Your decklist.** Paste in your 99 (or up to 100 if your site also includes the commander in
the maindeck — the tool deduplicates). Most common export formats are accepted:

- Plain `1 Card Name` — the default
- Arena `1 Card Name (SET) 123` — set code and collector number are stripped
- Archidekt/MTGO `1 Card Name [SET]` — bracket set code stripped
- Moxfield, EDHREC, and other sites that put a `# Section` header or a `Creatures (24)` count
  suffix — those headers are skipped
- Sideboard lines (`SB: ...`) are ignored

MDFCs (modal double-faced cards) are recognized by their front-face name alone — "Bala Ged Recovery"
resolves correctly even if the export omits the " // Bala Ged Sanctuary" back face.

**Your commander(s).** Picked via the same commander search as the builder, not pasted in — this
prevents mis-classification if the site included the commander in the 99.

### What you see

**Popular EDHREC themes** — shown above the result tabs once analysis completes. The top 8 themes
from EDHREC's tag data for your commander, ranked by deck count. Blue badges are themes the app
recognises (archetypes, mechanical themes, tribal types); grey badges are EDHREC-specific tags
without a direct app equivalent. Useful for comparing how the community typically builds this
commander against your actual card choices.

**Coverage Report** — how many cards you have in each functional role (Ramp, Card Advantage,
Disruption, etc.) vs. the balanced-deck baseline. Roles that fall short are highlighted.

**All Cards / By Type** — full card list with each card's classified role and secondary contributions,
sorted the same way as the deck builder results.

**Bracket Estimate** — a quick bracket guess (1–5, matching the Command Zone system) based on
tutor density and protection count. Useful to sanity-check whether a list reads as casual, mid-power,
or high-power.

**Role Gaps** — the roles most under-served in your current list, ordered by shortfall vs. the balanced
template. These feed directly into the Upgrade Paths tab.

### Upgrade Paths

Fill in the optional inputs and click **Get Upgrade Suggestions**:

- **What isn't working?** — Free text describing your frustrations ("I always lose to board wipes",
  "the deck is too slow"). This re-orders which gaps to address first before running upgrade selection.
- **Max price per card** — the tool only suggests cards at or under this price. Leave empty for no
  budget constraint.

If you fill these in before clicking **Analyze**, upgrades run automatically once analysis finishes.

For each of the top role gaps, the tool:

1. Fetches EDHREC recommendations for your commander(s) as the candidate pool.
2. Classifies the candidates with the same LLM step used in the builder.
3. Asks a fast cheap model (Haiku / Flash Lite) to re-rank the gaps based on your feedback text.
4. Asks your selected model to suggest up to three **add + cut** pairs for each gap — a specific card
   to add and a specific card from your current 99 to cut in its place, with written rationale for both.

All suggested adds are validated against the EDHREC pool (no hallucinated card names) and all suggested
cuts come from your actual decklist. Each unique commander is queried separately and the pools are merged,
so partner decks get recommendations from both halves of the pair.

---

## Current limitations

- **Context-aware classification** is not implemented. Some cards (e.g. Jeska's Will) behave
  very differently depending on whether the commander is on the board. Classification currently
  treats them as global-stable rather than commander-specific.
- **Results are browser-local.** The three most recent builds are saved to your browser's local
  storage. Clearing your browser data removes them. Use "Export Build Report" to keep a
  permanent copy of a finished deck.
- **Output varies.** The LLM is non-deterministic — the same inputs will not always produce
  the same deck. Classification results are cached within a session to reduce variation, but
  two separate runs are not guaranteed to agree.
