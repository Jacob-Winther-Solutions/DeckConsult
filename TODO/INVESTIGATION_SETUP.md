# Token Usage Investigation — Setup Complete

## What Was Built

**Goal:** Measure token usage per LLM call to identify why builds cost **$0.11–0.22** (target: **$0.01–0.05**).

### New Classes

**`EdhDeckBuilder.Agent/Instrumentation/UsageTracker.cs`**
- Captures per-call metrics: input/output tokens, cache hits/misses, model string, stage
- Formats detailed usage table for human review
- Computes aggregate cost estimate (Haiku pricing: $1/MTok input, $5/MTok output)
- Public API: `RecordCall(stage, model, usage)`, `GetCalls()`, `GetSummary()`, `FormatTable()`

### Modified Classes

**`ILlmClassifier` (LlmClassifier.cs)**
- Added `SetUsageTracker(UsageTracker)` method
- Records each `CallLlmAsync` call with stage="ClassifyBatch"

**`ICardSelector` (LlmSelector.cs)**
- Added `SetUsageTracker(UsageTracker)` method
- Records each selection call with stage=`Select-{role}` (e.g., "Select-Ramp")

**`IDeckBuilder` interface**
- Added optional property `UsageTracker? UsageTracker { get; set; }`

**`DeckBuilder` implementation**
- Wires up tracker to classifier and selector when set
- Automatically passes tracker from IDeckBuilder to its dependencies

### Test Suite

**`Tests/Agent/UsageInstrumentationTest.cs`**
- Marked `[Trait("Category", "Manual")]` — not run in normal test suite
- Two test methods:
  1. `MeasureUsage_Sephiroth_Aggro_Aristocrats()` — single control deck
  2. `MeasureUsage_WithThemes()` — multi-role midrange with themes
- Both print detailed usage table to console stdout

**Run with:**
```powershell
$env:ANTHROPIC_API_KEY="sk-ant-..."
dotnet test Tests --filter "Category=Manual" -v normal
```

## Expected Output Format

```
Call | Stage              | Model  | Input  | Output | CacheCreate | CacheRead | EstCost
─────┼────────────────────┼────────┼────────┼────────┼─────────────┼───────────┼─────────
 1   | ClassifyBatch      | haiku  | 18000  | 2500   | 18000       | 0         | $0.0305
 2   | Select-Plan        | haiku  | 8500   | 1200   | 0           | 8500      | $0.0128
 3   | Select-Ramp        | haiku  | 8200   | 1100   | 0           | 8200      | $0.0121
 4   | Select-Payoff      | haiku  | 7900   | 950    | 0           | 7900      | $0.0110
 ...
──────────────────────────────────────────────────────────────────────────────────────────
TOTAL  |                  |        | 152500 | 18700  | 18000       | 32600     | $0.2207
```

## How to Use

### Prerequisites
- Set `ANTHROPIC_API_KEY` environment variable or configure user secrets for `EdhDeckBuilder.Web`

### Quick Run
```powershell
$env:ANTHROPIC_API_KEY="sk-ant-...your key..."
dotnet test Tests --filter "Name=MeasureUsage_Sephiroth_Aggro_Aristocrats" -v normal 2>&1 | Tee-Object -FilePath usage-report.txt
```

### Programmatic Usage (for future enhancements)
```csharp
var tracker = new UsageTracker();
deckBuilder.UsageTracker = tracker;

var result = await deckBuilder.BuildAsync(...);

Console.WriteLine(tracker.FormatTable());
var summary = tracker.GetSummary();
Console.WriteLine($"Total cost: ${summary.EstimatedCostUsd:F4}");
```

## What To Do With The Report

Once you run the test and get the usage table:

1. **Count calls** — are there more than expected (~4–6 for a balanced build)?
   - If >10: check for retry loops or unintended re-calls

2. **Identify largest input** — which call consumes the most input tokens?
   - Classification typically 15–20k tokens
   - Selection per-role typically 8–10k tokens
   - If any call >25k, audit the prompt

3. **Check cache hits** — is `CacheReadInputTokens` > 0 on 2nd+ calls?
   - If always 0, caching isn't working — check prefix stability

4. **Measure total cost** — does it match your estimate?
   - Compare to usage notes in `AI-usages-personal-notes.md` ($0.11–0.22 typical)

5. **Identify biggest sink** — which role/stage costs the most?
   - May reveal that selection loop (Plan → Ramp → ... → Synergy) is the main cost driver

## Files Changed
- Created: `EdhDeckBuilder.Agent/Instrumentation/UsageTracker.cs` (150 lines)
- Modified: `EdhDeckBuilder.Agent/Llm/LlmClassifier.cs` (+3 lines, SetUsageTracker call)
- Modified: `EdhDeckBuilder.Agent/Llm/LlmSelector.cs` (+3 lines, SetUsageTracker call)
- Modified: `EdhDeckBuilder.Agent/Pipeline/DeckBuilder.cs` (+8 lines, UsageTracker property + wiring)
- Modified: `EdhDeckBuilder.Agent/Interfaces/IDeckBuilder.cs` (+5 lines, UsageTracker property)
- Created: `Tests/Agent/UsageInstrumentationTest.cs` (160 lines)

All changes are **isolated to instrumentation** — no business logic modified, no breaking API changes.

## Next Steps (After You Run The Tests)

1. Email or save the usage table to compare against hypothesis
2. Based on the bottleneck identified, apply targeted fix:
   - **If input tokens too high:** trim prompt (remove noise, use briefer context)
   - **If output tokens too high:** tighten schema, reduce rationale verbosity
   - **If cache not hitting:** check prompt prefix byte-stability, verify CacheControl set
   - **If too many calls:** merge selection roles or add batching
3. Re-run test and verify cost reduction toward $0.01–0.05 target

---

**Note:** Instrumentation added 2026-07-02. Tests are ready to run but require API key.
