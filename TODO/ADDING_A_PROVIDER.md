# Adding a New LLM Provider

A step-by-step guide for wiring in a third (or fourth) LLM provider. The architecture is
designed so that adding a provider means implementing a transport layer only — the three
shared adapters (`LlmClassifier`, `LlmSelector`, `LlmCommanderSelector`) already contain all
the business logic and need no changes.

---

## What you need to implement

### 1. An `ILlmClient` implementation

Create `EdhDeckBuilder.Agent/Llm/<Provider>/<Provider>HttpLlmClient.cs` implementing
`ILlmClient`:

```csharp
public interface ILlmClient
{
    Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default);
}
```

`LlmRequest` gives you everything you need:

| Field | Purpose |
|---|---|
| `Model` | Model ID string for this provider |
| `MaxTokens` | Output token ceiling |
| `Temperature` | Sampling temperature (may be null or unsupported — check first) |
| `SystemPrompt` | The system prompt string |
| `Messages` | Conversation turns (`LlmTextBlock`, `LlmToolUseBlock`, `LlmToolResultBlock`) |
| `Tools` | Tool definitions (name, description, input schema as `JsonNode`) |
| `ForcedToolName` | The tool to force-call (required — do not allow free-text output) |
| `EnableCaching` | Whether to apply prompt-cache hints (provider-specific, may be ignored) |

Return a `LlmResponse` with:
- `Content` — list of `LlmContentBlock` items; at minimum one `LlmToolUseBlock` with the
  tool name and the parsed `input` as a `JsonNode` (see `GeminiHttpLlmClient` for how to
  simulate this if your provider doesn't use tool-call format natively)
- `StopReason` — e.g. `"tool_use"`, `"end_turn"`, `"max_tokens"`
- `Usage` — `InputTokens`, `OutputTokens`, and optionally `CacheCreationInputTokens` /
  `CacheReadInputTokens`

**Error handling requirements:**
- 401 / 403 responses → throw `ApiKeyRejectedException` (wrapping an `HttpRequestException`)
- 429 / 502 / 503 / 504 → retry with `Retry-After`-aware backoff, up to 3 attempts
- MAX_TOKENS truncation (stop_reason = max_tokens / finish_reason = MAX_TOKENS) → return null
  payload or an empty tool input, and log a warning. Do not attempt partial-JSON recovery.

### 2. A factory that builds the client

Create `EdhDeckBuilder.Agent/Authentication/<Provider>/<Provider>LlmClientFactory.cs`
implementing `ILlmClientFactory`:

```csharp
public interface ILlmClientFactory
{
    ILlmClient CreateForCurrentUser();
    string ClassificationModel    { get; }
    int    ClassifierMaxOutputTokens { get; }
    string SelectedModel          { get; }
    int    SelectorMaxOutputTokens   { get; }
}
```

The factory is Scoped (one per Blazor circuit), so it can safely read the per-user
`SessionApiKeyProvider` to get the active key and selected model at call time.

Use `IHttpClientFactory` (via `AddHttpClient<YourFactory>()`) to obtain the `HttpClient` —
never construct `HttpClient` directly inside the factory or the client.

### 3. A model list

Create `EdhDeckBuilder.Agent/Authentication/<Provider>/<Provider>Models.cs` with `const string`
model IDs and a `SelectionModels` list for the UI picker. Mirror the shape of `ClaudeModels` or
`GeminiModels`.

### 4. Pricing entries

Add rows to `EdhDeckBuilder.Agent/Instrumentation/ModelPricing.cs`:

```csharp
{ "<provider-model-id>", new ModelRate(inputPerMTok: X.XX, outputPerMTok: Y.YY) },
```

Unknown models return a zero rate, so skipping this just produces a suspicious $0 in the
usage summary — not a crash, but misleading.

### 5. A key tester

Add a branch for your provider inside `KeyTester.TestAsync` in
`EdhDeckBuilder.Agent/Authentication/KeyTester.cs`. The interface:

```csharp
Task<KeyTestResult> TestAsync(string apiKey, AiProvider provider, CancellationToken ct = default);
```

Add an `if (provider == AiProvider.YourProvider)` block before the Anthropic path. Make the
smallest API call that will return 401 for a bad key and 200 for a good one — typically a
single-token completion. For now a format-only check is acceptable as a placeholder.

---

## DI wiring

All registration happens in
`EdhDeckBuilder.Agent/ServiceCollectionExtensions.cs` in `AddAgent()`.

Add `AddHttpClient<YourFactory>()` and register the factory as Scoped. Then extend the three
DI factory lambdas (one per interface: `ILlmClientFactory`, `ILlmClassifier`, `ICardSelector`,
`ICommanderSelector`) to check `ActiveProvider == AiProvider.<YourProvider>` and resolve your
factory:

```csharp
services.AddScoped<ILlmClientFactory>(sp =>
{
    var keys = sp.GetRequiredService<SessionApiKeyProvider>();
    return keys.ActiveProvider switch
    {
        AiProvider.Google       => sp.GetRequiredService<GeminiLlmClientFactory>(),
        AiProvider.YourProvider => sp.GetRequiredService<YourLlmClientFactory>(),
        _                       => sp.GetRequiredService<ClaudeHttpLlmClientFactory>(),
    };
});
```

Add `AiProvider.YourProvider` to the `AiProvider` enum in `Core` (or `Agent` —
wherever it currently lives).

---

## UI wiring

The API key entry UI is in `EdhDeckBuilder.Web/Components/Shared/ApiKeySettings.razor(.cs)`.
It reads `AiProvider` values to show the right label, placeholder, and help link. Add a tab /
case for your provider there.

The model picker for the selector is in the same component area. Add your `SelectionModels`
list to the picker in the same change.

Cookies for the API key and selected model are managed by `SessionApiKeyProvider` via
`IJSRuntime`. The provider discriminator is stored as a separate cookie (`edh-provider`).
Add your provider's cookie handling there.

---

## Constraints to preserve

- **Whitelist rule** — the three adapters already filter every `OracleId` in the response
  against the input batch. Your `ILlmClient` must make the raw `input` payload available as
  `LlmToolUseBlock.Input` (a `JsonNode`) so this filtering can run. Never return card names
  from the HTTP layer — return the full tool-input node.
- **Forced tool only** — `ForcedToolName` in `LlmRequest` is not optional. If your provider
  doesn't support native tool-call forcing, simulate it by setting `responseSchema` to match
  the tool's `InputSchema` and wrapping the parsed output in a `LlmToolUseBlock`.
- **Scoped lifetime** — the factory and client are Scoped. Never register them as Singleton
  (they capture the per-user key).
- **`IUsageTrackerAware`** — if you create a new adapter class (you probably won't, since the
  three shared ones handle everything), implement the marker interface and set the tracker in
  `SetUsageTracker`. If you only add a transport (`ILlmClient`), no change is needed — usage
  tracking is already handled by the adapters.
- **`ApiKeyRejectedException`** — map 401/403 at the transport level. The UI catches this,
  clears the key, and shows a reconnect prompt. Do not let it surface as a generic build
  failure.

---

## Checklist

- [ ] `ILlmClient` implementation with retry + 401 mapping
- [ ] `ILlmClientFactory` implementation (Scoped, uses `IHttpClientFactory`)
- [ ] `<Provider>Models.cs` with model ID constants and `SelectionModels` list
- [ ] `ModelPricing.Prices` entries for each new model ID
- [ ] `IKeyTester` implementation
- [ ] `AiProvider` enum extended
- [ ] `AddAgent()` DI lambdas updated (factory + all three interface resolvers)
- [ ] `ApiKeySettings` UI updated (tab, model picker, cookie handling)
- [ ] All 328+ tests still pass (`dotnet test Tests`)
