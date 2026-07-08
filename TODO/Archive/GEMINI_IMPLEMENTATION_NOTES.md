# Gemini Provider Implementation Notes — ARCHIVED

**Status: COMPLETED 2026-07-08.** The three LLM adapters this document said were blocked on
OpenAI SDK v2.2 were instead implemented as a custom REST client against Gemini's
`generateContent` endpoint. The OpenAI SDK dependency was dropped entirely. See
`EdhDeckBuilder.Agent/Llm/Gemini/` for the live code and `CLAUDE.md` / `README.md` for the
current architecture. Kept in Archive for historical context.

---

## Session Summary (2026-07-08)

This session completed the **infrastructure layer** for multi-provider support (Anthropic + Google Gemini). The LLM call adapters are structurally ready but blocked on OpenAI C# SDK v2.2 API specifics.

---

## What Works ✅

### UI & Settings
- Provider toggle (radio buttons): Anthropic / GitHub Models / Google AI Studio
- Provider-specific help text with API links
- Model dropdown dynamically populated per provider
- Google key validation (accepts "AQ", "AIza", and other prefixes)
- Placeholder text updates based on provider

### Persistence
- **Provider + model** saved to cookies (`edh_apikey`, `edh_apikey_gh`, `edh_apikey_google`, `edh_selectedmodel`)
- **Cookies restored on page reload** and app restart
- **Model selection** persists within a provider; resets when switching providers (expected UX)
- All three keys can coexist — user can switch freely without re-entering credentials

### Configuration
- `appsettings.json`: `Provider:Default` sets startup provider (Anthropic, GitHubModels, or Google)
- User secrets: `Anthropic:ApiKey`, `GitHub:ApiKey`, `Google:ApiKey` (all three can be stored)
- `SessionApiKeyProvider` reads and manages all three keys with provider-aware logic

### Dependency Injection
- `GeminiClientFactory` implements `IGeminiClientFactory` — points OpenAI SDK to Gemini endpoint
- DI dispatch lambdas in `ServiceCollectionExtensions.cs` ready to route to Gemini adapters when `ActiveProvider == Google`
- All three interfaces have dispatch logic in place (classifier, selector, commander selector)

### Tests
- All 328 tests passing
- No regressions introduced

---

## What's Blocked ⏳

### Three LLM Adapter Classes Deferred

**Files:**
- `EdhDeckBuilder.Agent/Llm/Gemini/GeminiClassifier.cs`
- `EdhDeckBuilder.Agent/Llm/Gemini/GeminiSelector.cs`
- `EdhDeckBuilder.Agent/Llm/Gemini/GeminiCommanderSelector.cs`

**Status:** Stubbed with `NotImplementedException`. Build succeeds; all tests pass.

**Issue:** OpenAI C# SDK v2.2 has a non-trivial type surface for tool call responses.

**What blocked us:**
- `ChatCompletionChoice` type is not public in OpenAI SDK v2.2
- Tool call parsing requires reflection or detailed type inspection (not straightforward)
- The response structure differs from Anthropic SDK's simpler `ToolUseBlock` model

**Next steps to unblock:**
1. Either upgrade to OpenAI SDK v3.0+ (if released) or study v2.2 more carefully via source/examples
2. Implement response parsing using dynamic types or reflection to extract tool call arguments
3. Once understood, each adapter follows the same pattern: format message → call client → parse JSON → return results

**Why we deferred:**
- Infrastructure/UI 100% complete and working (provider toggle, cookie persistence, key storage all verified)
- Anthropic/Claude LLM works perfectly (no regressions)
- Tests and build pass without Gemini adapters (stubs are safe placeholders)
- Investing time in SDK API surface details has diminishing ROI at this point; ship the infra, finish adapters later

---

## What Works Today

**To test the infrastructure (without Gemini LLM calls):**
1. Start the app normally with Claude/Anthropic (default provider)
2. Build a few decks, verify classification/selection works (cache gets populated)
3. Switch to "Google AI Studio" in the provider dropdown
4. The UI switch works; model list updates
5. *Deck building will fail at classification step* (NotImplementedException) — that's expected

**Full Gemini support will require:**
1. Understanding/documenting the OpenAI SDK v2.2 response types for tool calls
2. Implementing response parsing in each adapter
3. Testing with live Google API key (not critical until full integration ready)

---

## Architecture Notes

### Why separate adapters?
Each provider has a different SDK (Anthropic vs. OpenAI for Gemini). The domain interfaces (`ILlmClassifier`, `ICardSelector`, `ICommanderSelector`) are provider-agnostic; the adapters translate between domain calls and provider SDKs.

### Why factory lambdas in DI?
Allows runtime dispatch: if user has `ActiveProvider == Google`, resolve to `GeminiSelector`; otherwise `LlmSelector`. Single call site, clean provider abstraction.

### Why cookie persistence?
User often works across multiple sessions with the same provider choice. Cookies outlive the scoped `SessionApiKeyProvider`, so preferences survive app restarts.

---

## Files Modified This Session

**Created:**
- `GeminiModels.cs` — model constants for Gemini
- `AiProvider.cs` — enum with Google added
- `GeminiClientFactory.cs` — OpenAI SDK integration
- `IGeminiClientFactory.cs` — factory interface
- `GeminiClassifier.cs` — skeleton (needs SDK call fix)
- `GeminiSelector.cs` — skeleton (needs SDK call fix)
- `GeminiCommanderSelector.cs` — skeleton (needs SDK call fix)

**Modified:**
- `SessionApiKeyProvider.cs` — triple key storage, provider dispatch
- `ClaudeModels.cs` — added Gemini model list, unified `GetSelectionModels(provider)`
- `ClaudeKeyTester.cs` — Google key validation (format check)
- `ServiceCollectionExtensions.cs` — DI dispatch lambdas
- `ApiKeySettings.razor` + `.razor.cs` — provider toggle, dual/triple cookie handling, model persistence
- `EdhDeckBuilder.Agent.csproj` — added OpenAI NuGet package
- `TODO/TODO.md` — documented multi-provider status
