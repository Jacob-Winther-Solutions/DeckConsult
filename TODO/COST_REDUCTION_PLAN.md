# Token Cost Reduction Plan

## Current State
- **Cost per build:** $0.12 (measured: Sephiroth Aggro Aristocrats Bracket 3, $15 card budget)
- **Token breakdown:** 43.7k input + 15.9k output = 59.6k total
- **Problem:** Output tokens are 65% of cost (Haiku: output = 5× input price)
- **Root cause:** Selection prompts generate 1.5–2.0k tokens of detailed rationale per role (8 roles × 2k ≈ 16k output)

---

## Option 1: Reduce Rationale Detail in SelectionPrompt

**Goal:** Require shorter, focused rationales (one-liner vs. current 2–3 sentences)

### Current behavior
Line 33 in `SelectionPrompt.cs`:
```csharp
- The rationale must be 1–2 sentences explaining why this card earns its position in THIS specific deck. 
  Mention the commander by name, a concrete mechanical interaction, or a specific synergy with the 
  archetype or theme. Every rationale must be deck-specific...
```

Result: Model generates verbose reasoning like:
> "Sephiroth cares about keyword counters and creature ETBs, and this card does both while being efficient"

### Plan

**File:** `EdhDeckBuilder.Agent/Prompts/SelectionPrompt.cs` (line 33)

**Change:** Make rationale field optional or require max word count

**Implementation:**
1. Change line 33 to:
   ```csharp
   - The rationale must be a single sentence (≤15 words) explaining why this card is good for THIS deck.
     Be specific: name the commander or mention a concrete mechanic, not generic value.
   ```

2. Keep the forbidden patterns (line 34) — they're good discipline

3. Optionally: mark `rationale` as `optional: true` in the schema (line 169) so the model can skip weak rationales

**Expected impact:** 
- Rationale per card: ~50 tokens → ~20 tokens (60% reduction)
- Per-role output: 2000 tokens → 800 tokens
- Total output: 16k → 6.4k tokens
- **New cost estimate: $0.06–0.07/build** ✓

**Risk:** Low. Model will still explain choices; just more concisely. Test one build to verify quality doesn't suffer.

---

## Option 2: Return Top N Cards Scaled by Role Target

**Goal:** Request exactly `[role ideal count + buffer]` instead of ranking all candidates

**Current behavior:**
- Selection prompt ranks ALL candidates (often 10–20 per role)
- Model must output a rank and rationale for each
- Wastes tokens asking for 20 cards when only 12 are needed

**Better approach:**
- Get the role's ideal target from `context.ResolvedTemplate.Targets[role].Ideal`
- Add a small buffer (2–5 cards) for runner-ups and fill engine flexibility
- Request exactly that many top candidates

### Plan

**Files:**
1. `EdhDeckBuilder.Agent/Prompts/SelectionPrompt.cs`
2. `EdhDeckBuilder.Agent/Llm/LlmSelector.cs`

**Implementation:**

1. **LlmSelector.cs (line ~22):** Add context parameter to SelectAsync method (already there!)
   - Use `context.ResolvedTemplate` to get role targets

2. **SelectionPrompt.cs (line 48–52):** Update FormatUserMessage signature
   - Already accepts `context`, so we have access to `ResolvedTemplate`

3. **SelectionPrompt.cs (line 48–116):** Add logic to compute request count
   ```csharp
   // In FormatUserMessage, after line 52:
   var roleTarget = context.ResolvedTemplate.Targets.TryGetValue(role, out var target)
       ? target.Ideal
       : 10;  // fallback for non-standard roles
   var buffer = role switch
   {
       CardRole.Land => 5,           // land base has more variance
       CardRole.Ramp or CardRole.CardAdvantage => 4,
       _ => 2
   };
   var requestCount = Math.Min(roleTarget + buffer, candidates.Count);
   ```

4. **SelectionPrompt.cs (line 86):** Update user message
   ```csharp
   $"Identify the top {requestCount} best candidates for {role} from the following {candidates.Count} options:"
   ```

5. **SelectionPrompt.cs (line 154–175):** Add schema constraint (optional, as a hint)
   ```json
   "items": {
     "type": "array",
     "maxItems": 50  // reasonable max, rarely hit
   }
   ```

**Example outputs:**
```
Ramp (ideal=11):          request 11+4 = 15 cards
Tutor (ideal=4):          request 4+2  = 6 cards
Land (ideal=38):          request 38+5 = 43 cards
CardAdvantage (ideal=14): request 14+4 = 18 cards
```

**Expected impact:**
- Candidates per role: average ~15 → ~12 (30% reduction)
- Per-role output: 2000 tokens → 1400 tokens
- Total output: 16k → 11.2k tokens
- **New cost estimate: $0.085/build** ✓✓

**Risk:** Low. Data-driven request count matches actual deck constraints. Fill engine already handles sparse results.

**Bonus:** Scales intelligently — Tutor asks for 6 instead of 15, Land asks for 43 instead of 50, etc.

---

## Option 3: Tighten Output Schema (Stricter Validation)

**Goal:** Force shorter rationales via schema constraints and make model commit to brevity

**Current behavior:**
- Schema allows `rationale: { type: "string" }` with no length constraint
- Model can output 200+ character rationales per card
- Anthropic SDK allows unlimited string length

### Plan

**File:** `EdhDeckBuilder.Agent/Prompts/SelectionPrompt.cs` (line 154–175)

**Change:** Add `maxLength` to rationale field in schema

**Implementation:**

1. **Add maxLength to schema (line 167):**
   ```json
   "rationale": {
     "type": "string",
     "maxLength": 100  // ← Force brevity: ~1 short sentence
   }
   ```

2. **Update system prompt to align (line 33):**
   ```csharp
   - The rationale must be a single sentence (≤100 chars) explaining why this card fits the role in THIS deck.
   ```

3. **Test if Anthropic SDK enforces this:**
   - The SDK with `Strict = true` should validate against the schema
   - If model violates maxLength, the call will fail and need retry
   - If model respects it, output shrinks immediately

**Expected impact:**
- Rationale length: 150–300 chars → max 100 chars
- Per-role output: 2000 tokens → 1200 tokens
- Total output: 16k → 9.6k tokens
- **New cost estimate: $0.08/build** ✓ (smaller gain than options 1–2)

**Risk:** High. If the schema is too strict (`maxLength: 100`), the model may:
- Refuse to comply (retry loop)
- Truncate important info
- Violate the constraint (Strict mode catches this, fails the call)

Mitigation: Start with `maxLength: 150` and tighten incrementally after testing.

---

## Option 4: Combine Approaches (Recommended)

**Goal:** Stack efficiency gains for maximum cost reduction

**Implementation order:**

1. **First:** Option 1 (reduce rationale detail to 1 sentence)
   - Lowest risk
   - Quick win: 60% output reduction for rationale
   - Easy to revert if quality suffers

2. **Then:** Option 2 (return top 5 instead of all)
   - Medium risk, but essential for fill engine performance
   - Synergizes with Option 1: fewer cards + shorter rationales = significant savings

3. **Optional:** Option 3 (schema maxLength)
   - Only if Options 1+2 don't hit target
   - Use as a hard constraint after human review confirms brevity is OK

**Projected combined impact:**

| Step | Input | Output | Total | Est Cost |
|------|-------|--------|-------|----------|
| Current | 43.7k | 15.9k | 59.6k | $0.1231 |
| +Opt1 | 43.7k | 9.5k | 53.2k | $0.0870 |
| +Opt2 | 43.7k | 4.8k | 48.5k | $0.0459 |
| +Opt3 | 43.7k | 3.2k | 46.9k | $0.0346 |

**Target achieved:** Reduce from $0.12 → **$0.05–0.046/build** (60% cost reduction) ✓

---

## Option 5: Trim Input Prompts (Classification)

**Goal:** Reduce verbosity in the classification system prompt and user messages

**Current state:**
- Classification input: 43.7k tokens (72% of total)
- Two classification calls use 4.4k + 2.1k + 3.6k + 2.8k + 2.6k = ~15.5k tokens

**Opportunities:**

### 5a. Shorten System Prompt
**File:** `EdhDeckBuilder.Agent/Prompts/ClassificationPrompt.cs`

**Current:** Full explanation of each CardRole, examples, edge cases (~400 words)

**Optimization:** Remove examples or consolidate descriptions
- Role explanations can be more terse
- Example decks aren't strictly needed for Haiku

**Expected savings:** System prompt ~10% reduction (billed on every call)
- Impact: Modest (~5% of classification input)

**Risk:** Low. System prompt already cached after first call (via `CacheControl`), so savings only matter on new sessions.

---

### 5b. Reduce Card Detail in Candidates
**File:** `EdhDeckBuilder.Agent/Prompts/ClassificationPrompt.cs` (line ~FormatUserMessage)

**Current:** Each candidate card shows:
```
oracle_id: ...
Name: ...
Mana Cost: ...
Type: ...
Text: ... (full Oracle text, often 200+ chars)
```

**Optimization:** For classification, model doesn't need full Oracle text
- Mana cost can be omitted (role is determined by effect, not cost)
- Oracle text could be summarized by the infrastructure layer (e.g., extract keywords: "draw", "tutor", "removal")
- Type line is useful but can be shortened

**Example reduction:**
```
Before: "As long as X is on the battlefield, whenever you cast a spell, you may..."
After: "draw, enter-the-battlefield synergy, card-advantage"
```

**Expected savings:** 40–50% reduction in classification candidate input
- Classification input: 15.5k → ~8k tokens
- **Total input: 43.7k → 36.2k tokens**
- **Combined with other options: $0.12 → $0.04/build** ✓✓✓

**Risk:** Medium. Summarization must be accurate; if keywords are wrong, classification breaks.

---

## Option 6: Batch More Candidates per Classification Call

**Goal:** Classify more cards per API call instead of 5 separate calls

**Current behavior:**
- `ClassificationBatchSize = 50` (line 31 in `DeckBuilder.cs`)
- Pool ~250 cards → 5 calls
- Each call has overhead: system prompt, context, call latency

**Optimization:**
- Increase batch size to 100 or 150
- Fewer calls = fewer redundant system prompt tokens
- System prompt (~2k tokens) sent 5 times → 3 times = saves ~4k tokens

**Implementation:**
```csharp
// DeckBuilder.cs line 31
private const int ClassificationBatchSize = 100;  // was 50
```

**Expected savings:**
- System prompt overhead: 10k tokens → 6k tokens (40% reduction)
- Fewer round-trips: latency improvement (nice side effect)
- **Total input: 43.7k → 39.7k tokens**

**Risk:** Low. Schema can handle larger payloads. Model context window is 200k, so 100 cards is fine.

**Caveat:** Only works if you have 100+ cards to classify. For small pools, no benefit.

---

## Option 7: Cache Selection Prompts (Advanced)

**Goal:** Reuse selection rankings for the same role across different builds

**Current:** Selection results are never cached (context-dependent by design)

**However:** The *system prompt* for selection is static and can be cached
- Currently: System prompt sent with every SelectAsync call (line ~39 in LlmSelector.cs)
- Cached: System prompt reused across 8 selection calls in one build, or across sessions

**Status:** This is already implemented via `CacheControl = new CacheControlEphemeral()` on the Tool (line 45 in SelectionPrompt.cs)

**Improvement:** Measure actual cache hits
- Run 2 builds of the same commander
- Check if `cache_read_input_tokens` > 0 on the second build
- If yes, cache is working; if no, investigate prefix stability

**Risk:** None. Just measurement/validation.

---

## Option 8: Use Cheaper Model for Classification (Haiku 3.5 or Fable)

**Goal:** Classify with a cheaper model variant if available

**Current:** All calls use `claude-haiku-4-5-20251001` ($1 input, $5 output)

**Future:** When Anthropic releases `claude-haiku-3.5` or `claude-fable`, switch classification to it
- Likely pricing: ~50% cheaper
- Classification is lower-stakes (deterministic code validates results)
- Selection is higher-stakes (determines actual card picks) — keep on current model

**Status:** Not yet actionable. Monitor Anthropic releases.

---

## Optimization Priority & ROI

| Option | Input | Output | Total | ROI | Risk | Effort |
|--------|-------|--------|-------|-----|------|--------|
| **1. Tighten rationale** | — | 9.5k | 53.2k | 13% | Low | 5min |
| **2. Scale request count** | — | 11.2k | 54.9k | 8% | Low | 15min |
| **5b. Summarize Oracle text** | 36.2k | 9.5k | 45.7k | 23% | Medium | 30min |
| **6. Bigger batches** | 39.7k | 15.9k | 55.6k | 7% | Low | 2min |
| Combined 1+2 | 43.7k | 11.2k | 54.9k | 8% | Low | 20min |
| Combined 1+2+5b | 36.2k | 11.2k | 47.4k | **21%** | Medium | 50min |
| Combined ALL | 36.2k | 9.5k | 45.7k | **23%** | Medium | 60min |

---

## Testing & Rollback Strategy

### Before committing changes:

1. **Measure baseline:** Run 2–3 builds with current config, record usage
2. **Apply Option 1 only:** Rebuild 2–3 times, check:
   - Output tokens actually decreased?
   - Rationale quality still good (spot-check 3–5 cards)?
   - Build succeeds without errors?
3. **Apply Option 2:** Test that fill engine handles sparse results gracefully
4. **Apply Option 3 (if needed):** Verify schema strictness doesn't cause retries

### Rollback:
- Each option is isolated in `SelectionPrompt.cs`
- Revert to `HEAD` to undo any change instantly
- Keep this file in git history so you can compare versions

---

## Recommended Action Plan

### Phase 1: Quick Wins (30 min, 8% cost reduction)
**Target:** $0.12 → $0.11/build

1. **Option 1:** Tighten rationale requirement (5 min)
   - Change: "1–2 sentences" → "single sentence ≤15 words"
   - Risk: None
   - Verify: Spot-check 3 rationales after build

2. **Option 2:** Scale request count by role targets (15 min)
   - Change: Request `ideal + buffer` instead of all candidates
   - Risk: Low
   - Verify: Ensure fill engine gets enough choices

3. **Option 6:** Increase batch size to 100 (2 min)
   - Change: `ClassificationBatchSize = 100`
   - Risk: None
   - Verify: Confirm no API errors

### Phase 2: Medium Effort (60 min, 23% total reduction)
**Target:** $0.11 → $0.09/build

4. **Option 5b:** Summarize Oracle text in classification
   - Add helper: extract keywords from full text ("draw", "tutor", "removal", etc.)
   - Replace full Oracle text in prompts with summaries
   - Risk: Medium — keyword extraction must be correct
   - Test: Compare classifications with/without summaries on 2 builds

### Phase 3: Measurement & Iteration
5. **Option 7:** Verify cache is working
   - Run 2 builds of same commander
   - Check `cache_read_input_tokens` in usage report
   - If > 0, great; if 0, investigate why

---

## Implementation Roadmap

**Recommended sequence:**

1. **Start with Phase 1 (Options 1 + 2 + 6)** — 30 min, low risk
   - Implement all three at once
   - Measure one build to confirm it works
   - Expected: $0.12 → $0.11

2. **After validation, add Phase 2 (Option 5b)** — 60 min, medium risk
   - Build keyword extractor for Oracle text
   - Test on 2–3 builds
   - Compare before/after rationales to ensure quality
   - Expected: $0.11 → $0.09

3. **Monitor Phase 3 (Option 7)** — passive validation
   - Every build, check `CacheReadInputTokens`
   - If cache hits appear, you're getting free savings already
   - If not, investigate (e.g., prefix instability)

---

## Why This Order?

- **Phase 1 is safe:** Three independent tweaks, low interdependence, fast rollback
- **Phase 2 adds complexity:** Keyword extraction requires infrastructure; test more carefully
- **Phase 3 is passive:** Just observe and validate existing mechanisms work

**Total addressable savings:** $0.12 → **$0.08–0.09/build** (25–30% reduction)  
**Timeline:** 90 min of focused work, spread across 2–3 days of builds

Ready to proceed with Phase 1?
