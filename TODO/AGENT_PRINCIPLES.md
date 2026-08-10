# Agent Layer — Principles & Guardrails

Handoff / sanity-check document for the `EdhDeckBuilder.Agent` build.
This is **not** an implementation spec. It states the principles the agent must honor,
flags the decisions that are still open, and lists the failure modes to avoid.
Where something says **DECIDE**, do not improvise a hidden default — surface the choice.

---

## 0. One-line framing

The agent is **not** "an AI that builds a deck." It is a deterministic deck-building
**procedure** that consults an LLM only for the parts that require taste. This is the
direct extension of decisions already settled in Core: the LLM emits weights/judgments,
never structure; deterministic code owns the skeleton; Scryfall owns facts and legality.

---

## 1. Division of labor (non-negotiable)

| Concern | Owner |
|---|---|
| Resolved target distribution (counts/ranges/ideals per category) | Deterministic code |
| Color-identity legality, singleton, exact-100 count, curve sanity | Deterministic code |
| Card → role classification (`PrimaryRole` + overlaps) | LLM (cached) |
| Card selection / scoring to fill category targets | LLM |
| Per-card rationale for the UI | LLM (captured during selection) |

If a piece of logic has a correct answer that can be computed, it does **not** go to the LLM.
If it requires judgment about *this* commander/theme, it does.

---

## 2. Parameters collapse before the agent runs

By the time the agent executes, the four parameter types — **template, archetype, theme,
bracket** — have already been resolved (deterministically, upstream by the TemplateResolver)
into a single artifact:

- a **coverage target per role** (range + ideal), and
- a set of **soft constraints** (e.g. "favor low curve," "bracket power ceiling").

The agent never reasons about "Aggro" or "Aristocrats" as raw concepts. It sees
`Ramp: 9–11, ideal 10` and the soft constraints. Keep the fuzzy stuff out of the build loop.

**Coverage targets intentionally sum above 99** (the balanced baseline sums to ~107). This is
correct: a card with secondary roles satisfies multiple targets simultaneously, and deck sites
like Moxfield and Archidekt work exactly this way — categories can sum above 100, the physical
deck is still 100 cards. The TemplateResolver does NOT normalize to 99; the fill engine holds
the physical 99-card hard constraint.

---

## 3. Shape of the agent (bottom-up)

1. **BuildContext (immutable):** commander(s) with pre-classified `RoleProfile`s, color
   identity, resolved template with coverage targets already reduced by the commander's
   contribution (at 1.5× weight — see §13), bracket constraints, soft constraints.
2. **BuildState (mutable):** committed cards, each tagged with role + `RoleRelation`;
   running `PrimaryRole` counts; running `CoverageByRole`.
3. **Candidate pool:** a few hundred *legal, commander-relevant* cards from Infrastructure
   (EDHREC for relevance, Scryfall for facts). The agent selects **within** this pool.
4. **LLM consultation points (two):** classification and selection/scoring.
5. **Validator gate:** enforces exact-100, legality, singleton; triggers repair on violation.

### Orchestration: staged pipeline, not free-running tool loop

Prefer a **staged pipeline**: `resolve → gather pool → classify → fill → validate (→ repair)`,
with the LLM consulted at fixed stages. Reproducible, independently testable per stage,
no token wandering, and consistent with the "constrain the LLM to judgment" ethos.
A ReAct-style loop is more flexible but harder to trust around the invariants that matter most.
**DECIDE if deviating** — but the default is the pipeline.

---

## 4. Lands (was missing from the original category list)

Lands are **count-only** as a category (no extra support modeling baked into the template).
Construction is a **two-pass** approach:

- **Pass A — early:** reserve the land *count* (≈36–38 for a 99) so the spell engine builds
  against the correct nonland budget. Exact-100 math depends on this being fixed up front.
- **Pass B — late:** fixing / color-requirement / utility-land selection, after spells are known.

**DECIDE:** the precise land count and how Pass B interacts with utility lands that double as
removal or card advantage (do they consume a spell-category slot, a land slot, or count toward
coverage only?). Do not let this resolve implicitly — it directly affects the exact-100 invariant.

---

## 5. Fill is overlap-aware, NOT category-by-category independent

This is the genuine hard part of the engine. Filling Ramp to ideal and then Card Advantage to
ideal as independent steps will double-claim cards that serve both (e.g. Black Market Connections).

Rules:
- A card occupies **one physical slot** (`PrimaryRole`) but may satisfy **multiple coverage
  targets** at once (`CoverageByRole`). These two counting systems must never be conflated.
- **Fill order matters.** There must be a stated order and a stated reconciliation policy.
- **Reconciliation policy — DECIDE:** what happens when coverage is satisfied but physical
  count is short of 99, or physical count is full but a coverage target is unmet? There is no
  obvious-correct default. State the policy explicitly.

> Note: overlap support exists in Core; verify it is sufficient for the `Always` / `Modal` /
> `Transform` distinctions the fill loop relies on before building on top of it.

---

## 6. Treat every LLM-returned card name as untrusted input

The model will occasionally name a card that is not in the pool, not legal in the color
identity, or does not exist. Every pick **must** map back to a real Scryfall ID **from the
candidate pool**, and anything that fails to resolve is rejected. This is a **whitelist, not
a parse**. Code that trusts model card names directly is a bug even if the happy path works.

---

## 7. LLM behind an interface; classification cached

- Put the LLM seam behind interfaces (`IClassifier`, `ISelector` or equivalent) so the
  deterministic engine — resolver, validator, fill reconciliation — is **fully testable with
  mocks and zero API calls**. Most of the real logic lives here and deserves fast tests.
- **Cache classification — global-stable only.** Most roles are stable across commanders: Sol
  Ring is always Ramp, Path to Exile is always TargetedDisruption. Cache by `OracleId`.
- **Do not cache `Plan`, `Synergy`, or `Payoff` per-commander.** These roles are inherently
  context-dependent ("is this card the plan *for this commander*?", "does this pay off the
  strategy?"). Re-classify them per build rather than risking a stale global cache. The cost
  is acceptable since the candidate pool is small relative to all Scryfall cards.

---

## 8. Repair policy must exist before validation can fail

When the deck comes out at 101, or a color slips through:
- Prefer **deterministic repair** (e.g. trim the lowest-scored card) over re-asking the LLM
  to "remove two cards," which can cascade.
- LLM repair, if used at all, is a last-resort fallback.
- **DECIDE and state the policy.** It should be a decision, not whatever the model did first.

---

## 9. Reproducibility is not free

LLM output is non-deterministic; the Anthropic API has no real seed; EDHREC data shifts over
time. Same inputs will not produce the same deck twice. That's acceptable for a deck builder,
but it shapes testing:
- Golden/snapshot tests work for **deterministic stages** and for **classification of a fixed
  card set**.
- They do **not** work for end-to-end "this commander always yields this list."
- Do not write tests that assume determinism across the LLM seam.

---

## 10. Two smaller standing requirements

- **Capture a per-card rationale during selection.** Nearly free to ask for, feeds the
  role-grouped visual deck view, far cheaper to design in than to bolt on later.
- **Treat prompts as versioned, testable assets**, not string literals scattered through code.
  The classification and selection prompts *are* the encoded domain expertise.

---

## 11. Category difficulty is not uniform (review hint)

`Plan` is now a first-class `CardRole` in Core (alongside Ramp, CardAdvantage, etc.). It means
**the core strategy of the deck** — what the deck is actually trying to do: token-making spells
in a Tokens deck, equipment and auras in Voltron, combo pieces in Combo, aggro threats in Aggro.
In Note §13's set-cover framing, Plan is the category to maximise overlap for: every card that
advances the plan *and* provides ramp, or *and* provides card advantage, is worth more than a
card that only does one job. `CardRole.Utility` was removed — it had no clear purpose.

Categories vary in how much the LLM is doing:
- **Near-mechanical:** Ramp, TargetedDisruption, MassDisruption — classification is almost
  deterministic.
- **Highest-judgment:** **Plan** is the most commander-specific category; the LLM is doing
  nearly all the work. Review the "Plan" selection prompt far more critically than "Ramp".

---

## 12. The LLM seam (how the SDK calls actually look)

### Reframe: this is structured extraction, not agentic tool use

Both consultation points hand the model inputs and demand a **typed object** back. The model
does **not** decide when to act or loop over tools. "Build an agent with tools" pattern-matches
to a ReAct loop — the wrong idiom here. Make sure the seam is built as **structured extraction**.

### Three ways to get structured output (descending robustness)

1. **Native Structured Outputs** — the `output_format` parameter (behind a beta header) does
   grammar-constrained decoding and guarantees JSON-Schema compliance at the token level. The
   model literally cannot emit a shape you can't deserialize. **Preferred default for both calls.**
2. **Forced single tool call** — define one tool, force it via `tool_choice`, set `strict: true`,
   read the tool input as your JSON. Works, but it's a tool used as a JSON funnel. Safe fallback.
3. **Plain text + parse JSON yourself** — fragile. Avoid.

> **Beta-SDK caveat:** the C# SDK may lag the API on which mechanism is cleanly exposed. State the
> *principle* (prefer constrained/strict structured output over hand-parsing) and have Claude Code
> **check what the C# SDK actually surfaces** rather than assume. Forced-tool-call is the fallback.

### Call shapes

- **Classification:** input = a **batch** of candidate cards (Scryfall id + facts) + commander
  context. Output = `[{cardId, primaryRole, overlaps:[{role, relation}]}]`.
  - Batch many cards per call — per-card calls are a latency/cost disaster.
  - Every returned `cardId` **must** echo an id from the batch; reject anything else (whitelist).
- **Selection:** input = role-filtered classified pool + soft constraints. Output = **ranked**
  picks `[{cardId, rank/score, rationale}]`. Same id-validation rule. `rationale` feeds the UI.

### Review points (scrutinize Claude Code's code here)

- **Constrain vocabulary with enums.** `Role` and `RoleRelation` are JSON-schema enums. With
  strict/structured output an invalid role is impossible at decode time, not a runtime parse error.
  Free-text role fields are a defect even if the code runs.
- **The seam must NOT emit counts.** Selection returns *ranked cards*; deterministic code takes
  top N per the resolved target. No tool/field anywhere lets the model say "use 10 ramp." If one
  exists, it re-opens a settled decision (see §1, §2).
- **Interface speaks domain objects, not API types.** `IClassifier`/`ISelector` take/return Core
  models; SDK call, JSON schema, serialization stay hidden behind them. The JSON schema sent to the
  API should be **derived from** the C# DTOs so they can't drift.
- **Temperature + model are per-call levers.** Classification: low temperature, a cheaper/faster
  model is fine. Selection: more judgment, stronger model. Don't hardcode one model for both.
- **Prompt caching.** System prompt + candidate-pool facts are large and reused across calls.
  Cache them to cut cost.

---

## 13. Fill & reconciliation (the hard part)

### Reframe: fill is a constrained assignment problem, not a sequence of independent fills

Three things are in tension:
- **Physical slots** are the scarce, hard-constrained resource — exactly 99 after the commander,
  minus reserved land count. A budget you can neither exceed nor undershoot.
- **Coverage** is the objective — hit each category's ideal, within range.
- **Overlaps** are the lever that lets one scarce physical slot satisfy multiple coverage goals.

This is essentially weighted **set-cover**: spend 99 slots to cover targets as well as possible,
where some cards cover multiple targets. The failure mode of the naive approach follows directly:
filling Ramp to ideal, then Card Advantage to ideal **independently**, double-spends — it ignores
that a card committed for ramp may already advance card-advantage coverage, and that physical slots
are shared across all categories.

> **Do NOT reach for an ILP/optimization solver.** Over-engineered for fuzzy LLM-ranked inputs and
> unexplainable in a UI. Build a **greedy fill with principled ordering + a reconciliation pass**.
> The set-cover framing is for *understanding what greedy approximates and where it breaks* — not
> for implementing a solver.

### The biggest landmine: coverage credit per `RoleRelation`

Treating the three relations identically is a **modeling bug**, not a style choice. A wrong call
here yields a deck that looks balanced on the spreadsheet and plays short.

- **Always** — covers both roles *simultaneously* (Black Market Connections = ramp **and** draw at
  once). Full credit to both counters. The only relation that legitimately inflates
  `CoverageByRole` past physical count.
- **Modal** — either/or *at cast time* (Jeska's Will). Does one job per cast. Full credit to both
  counters overstates real coverage → discount or single-assign.
- **Transform** — sequential; second role usually *consumes* the card (Hedron Archive: ramp → sac
  for draw). Does both over a game, but not at once and not reliably → discount.

**Principle:** only `Always` gives full simultaneous credit; `Modal` and `Transform` must be
discounted or single-assigned. **DECIDE** the exact discount (count primary only / secondary at
0.5 / assign to neediest role) — but uniform summing across all three is wrong. **Flag hard for
review:** the relations already exist in Core, so summing them uniformly is the tempting default.

### Fill order follows from scarcity (greedy is order-dependent)

1. **Scarce categories before abundant ones.** "Plan" and Mass Removal have few, commander-specific
   candidates; Ramp has a huge, overlap-rich pool. Fill scarce first so you don't burn slots the
   thin categories needed.
2. **Overlap value is contextual to current state.** A ramp+draw card is worth its overlap bonus
   *only while both categories still need filling*. Once card-advantage is satisfied, that same card
   is just a ramp card. Recompute value against committed state **after every commit** — you cannot
   rank a global "best cards" list once and consume it.
3. **Fill `ideal − alreadyCovered`, not `ideal`.** When you reach Card Advantage, some is already
   covered by Always-overlap cards committed during Ramp. This subtraction is the central mechanism
   the naive approach omits.

### Reconciliation is a bounded swap loop

After greedy fill, three inconsistent states are possible:
- **Physical short, coverage met** (overlap was efficient): fill empty slots with best remaining
  flex cards, or push categories toward the top of their range.
- **Physical full, coverage short:** **swap** — cut from an over-covered category, add one covering
  the deficit. Recursive: the cut card may have supplied overlap coverage elsewhere, creating a new
  deficit.
- **Both off:** combination.

The danger is **oscillation** (cut A → under-covers Y → add B → over-covers X again). Two guarantees
prevent it:
- **Monotonicity:** every swap must strictly reduce total weighted deviation.
- **Hard iteration bound.** If no swap reduces deviation, stop, accept best-so-far, escalate to the
  repair policy (§8). *Reconciliation and repair are the same machinery.*

**Ranges are the slack reconciliation uses:** aim for ideal initially; move within range to satisfy
the hard 99 constraint; go *outside* a range only as a last resort, and **surface it as a signal**
(thin pool or incoherent template for this commander) — never silently accept it.

### Cross-cutting review points

- **Fill is deterministic given the classified + ranked pool.** All LLM judgment was injected
  upstream; fill is its deterministic consumer. Tiebreaks must be stable (e.g. EDHREC rank, then
  card id). Any RNG in fill is a defect — and it's what makes the §9 snapshot tests possible.
- **Dual-purpose lands couple §4 and fill.** A utility land that also does removal occupies a *land*
  slot but contributes *spell-category* coverage, retroactively reducing that category's needed
  spell slots. So Pass B cannot be fully independent of spell fill — either run a preliminary
  dual-purpose-land pass before spell fill, or iterate. This is the §4 **DECIDE** resurfacing inside
  the algorithm, and the subtlest coupling in the engine.

### Commander coverage contributes before fill begins

The commander is not one of the 99 physical slots, but it has a `RoleProfile` and it is always
available — it is effectively in your opening hand every game. Before the fill loop starts:

1. Classify the commander and build its `RoleProfile` (primary + secondary roles).
2. Apply its coverage contributions to the targets at an **amplified weight** (suggested: 1.5×
   — available ~1.5× as reliably as a card drawn once per game). For a partner pair, both
   commanders contribute at this multiplier.
3. Subtract that coverage from each target's needed amount: the fill loop fills
   `target[role] − commanderCoverage[role]`, not `target[role]`.

The exact multiplier is a **DECIDE** (see checklist). The principle is fixed: the commander
reduces what the 99 cards must provide, and it does so at higher-than-1.0 weight.

### Swap loop monotonicity constraint

The reconciliation swap loop must satisfy a **monotonicity guarantee**: every accepted swap
must strictly reduce total weighted deviation (sum of `|actual_coverage − ideal|` across all
roles). If no swap reduces deviation, stop immediately — do not try combinations or backtrack.
Accept the best-so-far state and escalate to the repair policy (§8). This is a hard algorithmic
constraint, not a suggestion: without it the loop can oscillate indefinitely.

### End-to-end shape

reserve land count → compute targets net of commander coverage (at 1.5× weight) → greedy fill
scarce→abundant, recomputing after each commit → check consistency → bounded monotonic
reconciliation swap loop within ranges → fill remaining physical slots → Pass B lands →
validate → repair if needed.

