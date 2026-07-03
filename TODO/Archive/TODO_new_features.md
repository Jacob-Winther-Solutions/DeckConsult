# TODO — New Feature Track: Deck Analyzer, Combo Finder, Locked Cards

**Status:** Concept-approved by Master, not yet scoped against the real codebase.

**Instructions for Claude Code:** This document describes *what* we've agreed to build and *why*, not a finished implementation spec. Treat every item below as a starting point for investigation, not drop-in code or a locked architecture. Before implementing anything:
1. Validate each item against the current state of `EdhDeckBuilder.Core`, `.Infrastructure`, `.Agent`, and `.Web`.
2. Flag anywhere an item conflicts with existing settled decisions (role taxonomy, `RoleRelation` types, `TemplateResolver` logic, color identity `[Flags]` enum, etc.) rather than silently reworking those decisions.
3. Where this doc is ambiguous or silent on a detail, propose an approach and confirm with Master before building — don't assume.
4. Break each feature into its own PR/commit sequence; do not bundle unrelated features together.

---

## 1. Deck Analyzer

**Goal:** Given an existing decklist (not built by our tool), classify it against our role taxonomy, estimate its bracket/power level, and generate staged budget upgrade paths.

### 1.1 Decklist ingestion
- [ ] Accept pasted decklist text in common export formats (at minimum: plain `1 Card Name` per line, Arena export format). Confirm with Master which export formats to prioritize (Moxfield, Archidekt, Arena, MTGO).
- [ ] Resolve each line to a Scryfall card via existing Scryfall client. Handle: fuzzy name matches, DFC/split cards, misprints/foreign names, cards not found (report to user, don't silently drop).
- [ ] Detect and separate the commander(s) from the 99 (may need explicit user input if the list format doesn't mark it).

### 1.2 Role classification
- [ ] Reuse the existing role-tagging logic from the deck builder pipeline (Ramp, Card Advantage, Targeted Removal, Mass Removal, Synergy, Plan, Tutor & Protection) to classify each card in the pasted list.
- [ ] Surface `CoverageByRole` totals for the pasted deck, same as used internally during building.
- [ ] Identify gaps: roles significantly under- or over-represented relative to our baseline template targets. Confirm what "significant" means quantitatively — reuse `TemplateResolver` baseline thresholds if they already encode this.
- [ ] Output: a report structure (not just UI) so this can be reused by both the "gap flagging" and "upgrade path" features below.

### 1.3 Bracket / power estimation
- [ ] Reuse/extend existing bracket logic (used in deck builder's bracket parameter) to run in reverse: given a deck, estimate its bracket.
- [ ] Confirm whether current bracket logic is generative-only (built for constraining output) or already reusable as an evaluator. If not reusable as-is, scope the refactor needed.
- [ ] Output should include a brief human-readable explanation of *why* the deck landed at that bracket (e.g., "contains N fast mana + M tutors"), not just a number.

### 1.4 Budget upgrade paths
- [ ] Given the classified/gapped decklist, generate staged upgrade suggestions at multiple budget tiers (e.g., current agreed tiers — confirm with Master; earlier discussion suggested something like $50/$150/$300 as illustrative, not final).
- [ ] Reuse existing card-selection/fill logic where possible rather than building a parallel selection system — this should feel like "the deck builder, but filling gaps in an existing list instead of from scratch."
- [ ] Each tier's suggestions should map to specific role gaps identified in 1.2.

**Open questions for Master before implementation:**
- Exact budget tier breakpoints.
- Which decklist export formats to support at launch.
- Whether bracket-estimation-in-reverse requires new logic or can reuse the generative bracket constraints.

---

## 2. Combo Finder

**Goal:** Given a decklist (built by us or pasted via the Analyzer), surface Commander Spellbook combos that are "close" — achievable with a small number of additional cards.

- [ ] Integrate Commander Spellbook REST API client in `EdhDeckBuilder.Infrastructure` (per `DATA_SOURCES.md` — confirm this integration hasn't already been scoped/started there).
- [ ] Given a decklist's card set, query for combos where most pieces are already present.
- [ ] Define and implement a "distance" metric — e.g., combos missing exactly 1–2 cards, ranked by fewest missing pieces first.
- [ ] Output: combo name/pieces, which pieces are owned vs. missing, and a short effect description (pull from Spellbook API, respect their licensing/attribution requirements).
- [ ] Integration point 1: standalone check against a pasted decklist (pairs with Deck Analyzer).
- [ ] Integration point 2: optional signal during deck building — confirm with Master whether this should influence card selection in v1, or stay read-only/informational for now. Recommend starting read-only to avoid entangling with `TemplateResolver`'s deterministic fill logic.

**Open questions for Master before implementation:**
- Whether v1 is read-only (analysis/discovery only) or should feed into build-time selection.
- Attribution/display requirements from Commander Spellbook's licensing terms (re-verify current terms before shipping).

---

## 3. Locked / Included Cards

**Goal:** Let the user specify cards that must appear in the generated deck regardless of budget, theme, or archetype constraints — for pet cards or cards the user already owns.

- [ ] Add a locked-card list input to the Deck Builder flow (pasted list, same ingestion/resolution path as Analyzer's 1.1 — reuse, don't duplicate).
- [ ] Validate locked cards against the chosen commander's color identity (`IsWithin` check) before build starts; reject or warn on illegal inclusions rather than silently dropping them.
- [ ] Locked cards must be:
  - [ ] Reserved as fixed slots before the deterministic fill pass runs.
  - [ ] Counted toward `CoverageByRole` for whatever role(s) they fill, so the fill pass doesn't over-provision an already-covered role.
  - [ ] Excluded from the budget cap calculation (confirm with Master: fully excluded, or counted but the *remaining* budget is what's distributed to the rest of the deck — these are different behaviors and the doc currently assumes the latter based on the "own an expensive card, rest stays budget" use case).
- [ ] Confirm interaction with land count / Pass A / Pass B land logic if a locked card is itself a land.
- [ ] Confirm interaction with `RoleRelation` types (`Always`/`Modal`/`Transform`) — a locked card with multiple roles should still resolve correctly.

**Open questions for Master before implementation:**
- Exact budget semantics (excluded entirely vs. deducted from total).
- Any cap on number of locked cards (e.g., should we warn if locked cards alone exceed 99, or leave excess as a hard build error?).

---

## Explicitly out of scope for this pass
- Partner/Background commander support (separate, already on Master's roadmap).
- True collection import via persistent storage (no storage layer exists yet; locked-card input is a per-run manual workaround, not collection tracking).
- Any changes to Brawl builder, Duel Commander, or other format expansion work.
