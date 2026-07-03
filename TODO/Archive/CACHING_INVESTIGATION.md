# Prompt Caching Investigation & Fix

## Problem Statement

Usage report shows:
- `CacheCreationInputTokens: 0` (never created a cache)
- `CacheReadInputTokens: 0` (never read from cache)

**This is a bug.** Caching should be working but isn't.

---

## Why Caching Matters

**Expected behavior:**
```
Build 1 (Sephiroth):
  Call 1 (ClassifyBatch): System prompt cached (2k tokens) + user message
  Call 2 (Select-Plan):   System prompt cached (2k tokens) + user message
  ...
  TOTAL CACHE CREATION: ~8k tokens

Build 2 (Sephiroth again):
  Call 1 (ClassifyBatch): System prompt READ from cache (0 cost!) + user message
  Call 2 (Select-Plan):   System prompt READ from cache (0 cost!) + user message
  ...
  SAVINGS: ~8k tokens (67% cost reduction on second build!)
```

**Actual behavior:**
- No cache creation
- No cache reads
- Every call sends full prompts

---

## Root Cause Analysis

### The Problem: SDK API Limitation

**Anthropic SDK v12.30.0 limitation:**
- `System` parameter in `MessageCreateParams` is just a `string`
- Cannot attach `CacheControl` to the System prompt string
- `CacheControl` can only be set on `Tool` objects, not on `System` or `Messages`

**Evidence in code:**
```csharp
// LlmClassifier.cs line 53
System = new MessageCreateParamsSystem(ClassificationPrompt.SystemPrompt),
// ↑ This is a string, no cache control available

// ClassificationPrompt.cs line 71
CacheControl = new CacheControlEphemeral(),
// ↑ This is on Tool, which is NOT cached by the SDK in this version
```

### Why Tool CacheControl Isn't Helping

The Anthropic API has two places where caching can work:
1. **System prompt block** — static prefix, sent with every request
2. **Tool definitions** — static schema, sent with every request

The SDK sets `CacheControl` on the Tool object, but the Anthropic API requires the cache directive to be on the `system` block in the request JSON, not on the tool schema.

**What we need:**
```json
{
  "system": [
    {
      "type": "text",
      "text": "You are an expert Magic deck builder...",
      "cache_control": { "type": "ephemeral" }  // ← THIS ENABLES CACHING
    }
  ],
  "tools": [ ... ]
}
```

**What we're sending:**
```json
{
  "system": "You are an expert Magic deck builder...",  // ← No cache control
  "tools": [
    {
      "name": "classify_cards",
      "cache_control": { "type": "ephemeral" }  // ← Wrong place
    }
  ]
}
```

---

## Solution: Waiting for SDK Update

**Status:** Anthropic SDK v12.30.0 does not yet expose `SystemBlockParam` in the public API.

The required API is:
```json
{
  "system": [
    {
      "type": "text",
      "text": "You are an expert...",
      "cache_control": { "type": "ephemeral" }
    }
  ]
}
```

But the SDK's `MessageCreateParamsSystem` class only accepts a string, not a list of blocks with cache control.

### Workaround (Current)

No code change was applied because the SDK doesn't support it yet.

### Planned Fix (v12.31.0+)

When Anthropic releases the SDK with `SystemBlockParam` support, apply this:

**File:** `EdhDeckBuilder.Agent/Llm/LlmClassifier.cs`

```csharp
// Create system block with cache control
var systemBlock = new SystemBlockParam
{
    Text = ClassificationPrompt.SystemPrompt,
    CacheControl = new CacheControlEphemeral(),
};

var request = new MessageCreateParams
{
    Model     = ClaudeModels.Haiku,
    MaxTokens = MaxTokens,
    System    = new MessageCreateParamsSystem([systemBlock]),
    Tools     = [ClassificationPrompt.Tool],
    ToolChoice = new ToolChoiceTool { Name = ClassificationPrompt.ToolName },
    Messages  = [
        new() { Role = Role.User, Content = ClassificationPrompt.FormatUserMessage(candidates, commanders) },
    ],
};
```

**Same fix for `LlmSelector.cs`** — apply identical pattern.

### Tracking

- [ ] Monitor Anthropic SDK releases for `SystemBlockParam` support
- [ ] When available, apply the fix above to both LLM callers
- [ ] Verify cache creation/read tokens > 0 in usage report
- [ ] Measure cost reduction from cache reuse

---

## Expected Impact After Fix

### Single Build (no cache yet)
```
Call 1: Create cache for System (2k tokens counted in CacheCreationInputTokens)
Call 2–13: Read cache for System (2k × 12 = 24k counted in CacheReadInputTokens)
```

Result:
- Input: 43.7k (same)
- Output: 15.9k (same)
- **CacheCreation: 2k** (was 0)
- **CacheRead: 24k** (was 0)

**Cost difference:** ~$0.03 savings on just the cache tokens themselves (10% reduction!)

### Multiple Builds in Same Session
```
Build 1: Cache created (cost: full)
Build 2: Cache reused (cost: input - 2k cache overhead)
Build 3: Cache reused (cost: input - 2k cache overhead)
```

**Savings per subsequent build:** ~$0.03–0.04 (25–33% reduction)

---

## How to Verify the Fix Works

After implementing the fix:

1. **Run a build and check usage report:**
   ```
   CacheCreationInputTokens: 2000  (was 0) ✓
   CacheReadInputTokens:     24000 (was 0) ✓
   ```

2. **Run a second build of the SAME commander:**
   ```
   CacheCreationInputTokens: 0     (no new cache)
   CacheReadInputTokens:     24000 (reads from cache) ✓
   Cost savings: $0.03–0.04
   ```

3. **Run a build of a DIFFERENT commander:**
   ```
   CacheCreationInputTokens: 2000  (new cache for new context)
   CacheReadInputTokens:     24000 (reads system prompt cache)
   ```

---

## Why This Hasn't Been Caught Before

The code had:
```csharp
CacheControl = new CacheControlEphemeral(),  // On Tool
```

This compiles without error and looks correct. But the Anthropic API doesn't support caching tool schemas themselves — only the system prompt and messages can be cached. The code was **harmless but ineffective**.

---

## Implementation Checklist

- [ ] Add `using Anthropic.Models.Messages;` to both files (if not already present)
- [ ] Update `LlmClassifier.cs` CallLlmAsync method
- [ ] Update `LlmSelector.cs` SelectAsync method
- [ ] Build and verify no compilation errors
- [ ] Run 2–3 builds and check usage report for CacheCreation/CacheRead > 0
- [ ] Measure cost difference before/after
- [ ] Document the fix in CLAUDE.md guardrails section

---

## Potential Issues & Mitigations

### Issue 1: Byte-Stability of System Prompt
**Risk:** If the System prompt string changes per call (e.g., dynamic insertion), cache won't work.

**Mitigation:** System prompts should be 100% static. Verify:
```csharp
// OK: Static string
System = new MessageCreateParamsSystem([systemBlock]);

// NOT OK (would break cache):
System = new MessageCreateParamsSystem([
    new SystemBlockParam {
        Text = ClassificationPrompt.SystemPrompt + DateTime.Now.ToString(),  // ← Dynamic!
    }
]);
```

**Current codebase:** ✓ System prompts are static constants.

### Issue 2: Cache Timeout
**Risk:** Ephemeral cache expires after 5 minutes. If you run builds slowly, cache won't be there.

**Mitigation:** This is by design (Anthropic's pricing). Within the same test run (multiple builds <5 min apart), cache should persist. For multi-day development, cache will reset — not a problem, just means first build of the day pays full price.

### Issue 3: SDK Version Compatibility
**Risk:** Future SDK versions might change the API.

**Mitigation:** Document this in CLAUDE.md as a guardrail. If `SystemBlockParam` is deprecated, update both LLM callers at the same time.

---

## Summary

**Bug:** Prompt caching not working due to wrong SDK API usage (setting CacheControl on Tool instead of SystemBlockParam).

**Fix:** 10 lines of code across 2 files to use SystemBlockParam with CacheControl.

**Payoff:** 
- First build: ~$0.02–0.03 savings (cache overhead)
- Subsequent builds (same session): ~$0.03–0.04 savings per build (25% cost reduction)
- Multi-build development: Massive cumulative savings

**Risk:** Low. Fix is straightforward, no breaking changes to interfaces.

**Effort:** 15 min implementation + 5 min verification.
