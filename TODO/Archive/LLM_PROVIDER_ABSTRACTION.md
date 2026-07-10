# LLM Provider Abstraction — Direct HttpClient Design

## Status
Design confirmed by Master. Ready for Claude Code implementation.

## Guardrails for Claude Code
- Do **not** reintroduce the Anthropic C# SDK or the Gemini SDK. The whole point of this doc is to remove both in favor of direct `HttpClient` calls.
- Do **not** redesign the `ILlmClient` interface shape or the DTO contracts below without flagging it back to Master first — these were settled conversationally.
- Do **not** try to fully unify provider request/response shapes into one JSON schema. The abstraction lives at the C# interface level; each adapter owns translating to/from its provider's native wire format.
- Preserve the existing BYOK seam (`SessionApiKeyProvider`, `IClaudeClientFactory`, `ClaudeKeyTester`) — this doc extends it to be provider-aware, it does not replace it.
- Open questions are marked `[OPEN]`. Do not silently resolve these — surface them.

---

## 1. Purpose & Context

DeckConsult currently talks to Anthropic via the community C# SDK and to Gemini via direct REST (because the Gemini SDK was found lacking). This creates two different integration patterns in the codebase and blocks a real diagnosis of the prompt-caching issue, since the Anthropic SDK's request/response handling is opaque to us.

Goal: replace the Anthropic SDK with a direct `HttpClient` implementation, and unify both providers behind one internal abstraction so the rest of the app (Agent layer, BYOK tester, etc.) doesn't need to know which provider a session is using.

## 2. Goals

- One C# interface (`ILlmClient`) that the Agent layer and BYOK plumbing code against, regardless of provider.
- Provider-specific quirks (system prompt shape, tool schema, streaming events, caching mechanics) fully absorbed inside each adapter — never leaking into calling code.
- Raw request/response visibility (behind a debug flag) for both providers, to finally diagnose the Anthropic caching issue and to make future provider debugging easier.
- Clean extension point for adding a third provider later without touching calling code.

## 3. Non-Goals

- Not unifying the two providers' JSON wire formats into a single schema. Anthropic's Messages API and Gemini's generateContent API differ structurally (see Section 6) and forcing a shared wire format would mean losing provider-specific capabilities (e.g. Anthropic's `cache_control`) or building a lossy translation layer. The shared surface is the C# interface, not the JSON.
- Not implementing multi-provider fallback/routing logic in this pass. That's a separate feature if wanted later.
- Not touching the free/Pro/Unlimited tier logic or `SessionApiKeyProvider`'s key-resolution rules — this doc only changes *how* a resolved key is used to make a call.

---

## 4. Core Abstraction

```csharp
public interface ILlmClient
{
    Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default);

    // [OPEN] Streaming: do we need streaming support for any current
    // DeckConsult feature (e.g. Rules Assistant guided conversation)?
    // If yes, add: IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken ct = default);
}
```

### DTOs (provider-agnostic)

```csharp
public sealed class LlmRequest
{
    public required string Model { get; init; }
    public required int MaxTokens { get; init; }
    public string? SystemPrompt { get; init; }
    public required IReadOnlyList<LlmMessage> Messages { get; init; }
    public IReadOnlyList<LlmToolDefinition>? Tools { get; init; }
    public bool EnableCaching { get; init; } // adapter decides how/whether to honor this
}

public sealed class LlmMessage
{
    public required LlmRole Role { get; init; } // User, Assistant
    public required IReadOnlyList<LlmContentBlock> Content { get; init; }
}

// Discriminated union in practice via abstract base + subtypes
public abstract class LlmContentBlock { }
public sealed class LlmTextBlock : LlmContentBlock { public required string Text { get; init; } }
public sealed class LlmToolUseBlock : LlmContentBlock { public required string ToolName { get; init; } public required JsonNode Input { get; init; } }
public sealed class LlmToolResultBlock : LlmContentBlock { public required string ToolUseId { get; init; } public required string Result { get; init; } }

public sealed class LlmToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonNode InputSchema { get; init; }
}

public sealed class LlmResponse
{
    public required IReadOnlyList<LlmContentBlock> Content { get; init; }
    public required LlmUsage Usage { get; init; }
    public required string StopReason { get; init; }
}

public sealed class LlmUsage
{
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }
    public int? CacheCreationInputTokens { get; init; } // null/0 for providers without this concept
    public int? CacheReadInputTokens { get; init; }
}
```

Notes:
- `LlmUsage` deliberately includes the Anthropic-specific cache fields as nullable, rather than inventing a fake equivalent for Gemini. Calling code checks for null rather than assuming both providers report the same thing.
- `EnableCaching` is a request-level *hint*. The Anthropic adapter decides where to attach `cache_control`; the Gemini adapter can no-op it or wire it to Gemini's explicit cache API later (see Section 6).

---

## 5. Provider Adapters

### 5.1 `AnthropicHttpLlmClient`

Responsibilities:
- POST to `https://api.anthropic.com/v1/messages`.
- Headers: `x-api-key`, `anthropic-version: 2023-06-01`, `content-type: application/json`.
- Translate `LlmRequest.SystemPrompt` → top-level `system` field (as a string, or as a content-block array with `cache_control` on the last block if `EnableCaching` is true and the prompt is long enough to be worth caching — see caching note below).
- Translate `Tools` → Anthropic's `tools` array shape (`name`, `description`, `input_schema`).
- Translate response `content` blocks (`text`, `tool_use`) back into `LlmContentBlock` subtypes.
- Map `usage.cache_creation_input_tokens` / `usage.cache_read_input_tokens` straight through to `LlmUsage`.
- Log raw outgoing JSON and raw incoming JSON when a debug flag is set (see Section 7).

Caching specifics to get right (this is the bug we're chasing):
- `cache_control: { "type": "ephemeral" }` goes on the *content block*, not the message or request root.
- The cacheable prefix must meet the model's minimum token threshold or caching silently no-ops with no error — confirmed minimums should be pulled from current docs rather than hardcoded, since these vary by model and can change.
- Only cache stable, reused prefixes (e.g. system prompt / card data context), never the per-request user turn.
- Verify in the raw response `usage` block whether `cache_creation_input_tokens` (first call, writing to cache) or `cache_read_input_tokens` (subsequent calls, reading from cache) is populated as expected. If both are 0 on a call that should have hit cache, that's the signal the prefix didn't qualify.

### 5.2 `GeminiHttpLlmClient`

Responsibilities:
- POST to Gemini's `generateContent` (or `streamGenerateContent`) endpoint for the configured model.
- Translate `LlmRequest.SystemPrompt` → Gemini's `systemInstruction` field (different shape than Anthropic's `system`).
- Translate `Tools` → Gemini's function-declaration schema (different field names/nesting than Anthropic's `input_schema`).
- Translate Gemini's response parts back into `LlmContentBlock` subtypes.
- Gemini has no per-request inline caching equivalent to `cache_control`. If `EnableCaching` is true, this adapter either no-ops it for now, or (future work) wires it to Gemini's explicit cache-object API, which requires creating a cache resource up front and referencing its ID in the request — structurally different from Anthropic's inline hint. `[OPEN]`: decide whether Gemini caching is worth implementing now or deferred.
- Log raw outgoing JSON and raw incoming JSON when the same debug flag is set.

---

## 6. Where "Same Pattern" Does and Doesn't Apply

| Concern | Anthropic | Gemini | Unified? |
|---|---|---|---|
| Transport | `HttpClient` POST, JSON | `HttpClient` POST, JSON | Yes — same `HttpClient`-based adapter pattern |
| Auth | `x-api-key` header | API key as query param or header (confirm current Gemini convention) | Adapter-internal, not exposed |
| System prompt | Top-level `system` field | `systemInstruction` field | No — different shape, translated in adapter |
| Tool schema | `tools[].input_schema` | Function declarations, different nesting | No — translated in adapter |
| Streaming | SSE with typed events (`message_start`, `content_block_delta`, etc.) | Different streaming shape | No — if streaming is needed, each adapter implements its own parser behind the same `IAsyncEnumerable` |
| Caching | Inline `cache_control` hint per content block | Explicit cache-object API (separate call) | No — fundamentally different mechanics, see 5.2 |

The unification is real and valuable at the **interface and DTO level** — Agent layer code, BYOK key testing, and usage logging all become provider-agnostic. It is intentionally *not* real at the wire-format level.

---

## 7. Debug Logging

Add a debug flag (config-driven, off by default in production) that, when enabled, logs:
- The exact outgoing JSON body per request.
- The exact raw response JSON per response, before DTO translation.
- Which adapter (`AnthropicHttpLlmClient` / `GeminiHttpLlmClient`) handled the call.

This is cheap to add now and is the direct tool for closing out the caching diagnosis — it lets us confirm whether `cache_control` is serialized correctly and what the real `usage` values are, independent of the DTO translation layer.

`[OPEN]`: where should these logs go — structured logging sink, or a simple rolling file for now? Depends on whatever logging infra already exists in `.Infrastructure`.

---

## 8. Integration with Existing BYOK Seam

- `IClaudeClientFactory` becomes (or is joined by) a provider-aware factory, e.g. `ILlmClientFactory`, that returns an `ILlmClient` based on the session's configured provider (Anthropic vs Gemini) and resolves the key via the existing `SessionApiKeyProvider`.
- `ClaudeKeyTester` logic (validating a BYOK key works) generalizes to an `ILlmClient`-based tester that works for either provider, since it just needs to fire a minimal request and check for a valid response vs auth error.
- No changes to the free/Pro/Unlimited tier decision logic — this is purely about *how* a call is made once a key and provider are already resolved.

---

## 9. Migration Plan

1. Introduce `ILlmClient`, DTOs, and both adapters alongside the existing SDK-based code (no behavior change yet).
2. Point the caching diagnostic work at `AnthropicHttpLlmClient` directly to close out the open caching bug, since this is the most urgent unblock.
3. Once `AnthropicHttpLlmClient` is verified correct (including caching), swap the Agent layer over to `ILlmClient` for Anthropic calls.
4. Swap Gemini calls over to `GeminiHttpLlmClient`, retiring whatever ad hoc REST code currently handles Gemini.
5. Remove the Anthropic SDK package reference once nothing depends on it.

## 10. Open Questions Checklist

- [ ] Do we need streaming support in this pass, or can it be deferred? (Section 4)
- [ ] Is Gemini explicit caching worth implementing now, or deferred until it's actually needed? (Section 5.2)
- [ ] Where should debug request/response logs be written? (Section 7)
- [ ] Confirm current Gemini auth convention (header vs query param) before implementation.
- [ ] Confirm current Anthropic per-model minimum cacheable prefix length against live docs rather than hardcoding a remembered value, since this can change.
