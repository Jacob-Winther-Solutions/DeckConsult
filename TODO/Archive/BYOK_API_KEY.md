# BYOK — Per-User Anthropic API Keys

> **Read this first.** This is a design spec produced from a planning conversation.
> The author of this spec does **not** have access to the current state of the
> codebase. **Adapt everything below to the real implementation**: the Anthropic
> SDK's actual client/request types, the existing `IClassifier`/agent abstractions
> and their real method signatures, DI conventions, and the Blazor hosting model.
> Treat code blocks as *illustrative drafts*. The SDK calls in particular
> (`AnthropicClient`, `Messages.CreateAsync`) are placeholders — wire them to
> whatever the beta SDK actually exposes. Do **not** redesign the settled agent
> architecture (staged pipeline, LLM-as-structured-extraction, the
> `IClassifier`/`ISelector` seams) to accommodate these snippets.

## Purpose

To make the app publicly usable without us paying for everyone's inference, each
user supplies **their own Anthropic API key**, billed to their own account
("Bring Your Own Key"). This replaces the single company key currently read from
configuration. The key must be resolved **per user at request time**, never baked
into a singleton client.

## Why BYOK (verified external facts — re-verify before launch)

- **Anthropic offers no third-party OAuth.** As of early 2026 their OAuth flow is
  locked to Claude Code and Claude.ai. There is no "Log in with Anthropic" we can
  integrate, and no way to register a third-party client ID.
- **Subscription tokens are off-limits.** Using OAuth tokens from Free/Pro/Max
  plans in a third-party tool is prohibited by the Consumer Terms (Feb 2026).
- **Routing all users through our single Console key is also restricted** by the
  commercial terms (the "wrapper" pattern) — and would put all usage cost on us.
- **The sanctioned path is BYOK:** each user creates a key in the Anthropic
  Console; usage bills directly to their account at pay-per-token rates. Keys
  carry the `sk-ant-` prefix.

## Hosting assumption

Design assumes **Blazor Server**, so the per-user key lives **server-side only**
(in the circuit's scoped service) and never reaches the browser. If the project
is Blazor WASM, this design must change: the key would live in the browser and
calls would go direct to Anthropic (requiring the direct-browser-access header
and exposing the key client-side). Prefer Server, or proxy calls through the
server. **Confirm the hosting model before implementing.**

## Proposed design

Lives in the **Agent** project. Two small abstractions:

1. A **scoped key provider** holding the current user's key (populated by the
   settings UI). Scoped == one circuit == one user session in Blazor Server.
2. A **client factory** that is the single seam touching the SDK — keeps the SDK
   out of the rest of the agent and makes the classifier trivially testable
   (consistent with the existing `IClassifier` abstraction).

Plus an optional **key tester** so the settings page can validate a key on entry.

### Key provider

```csharp
namespace EdhDeckBuilder.Agent.Authentication; // adapt to real convention

/// Supplies the CURRENT user's key. Register Scoped.
public interface IClaudeApiKeyProvider
{
    string? GetApiKey();   // null until the user connects their own key
}

public sealed class MissingApiKeyException()
    : Exception("No Anthropic API key is connected for this session.");

/// Per-circuit holder. The settings page calls Set(...) after the user pastes
/// their key; the agent reads it through the interface. In-memory only —
/// nothing persisted to disk here.
public sealed class SessionApiKeyProvider : IClaudeApiKeyProvider
{
    private string? _key;
    public string? GetApiKey() => _key;

    public void Set(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) ||
            !apiKey.Trim().StartsWith("sk-ant-", StringComparison.Ordinal))
            throw new ArgumentException("That doesn't look like an Anthropic API key.");
        _key = apiKey.Trim();
    }

    public void Clear() => _key = null;
}
```

### Client factory (the SDK seam)

```csharp
public interface IClaudeClientFactory
{
    AnthropicClient Create(string apiKey);     // type name illustrative
    AnthropicClient CreateForCurrentUser();
}

public sealed class ClaudeClientFactory(IClaudeApiKeyProvider keys) : IClaudeClientFactory
{
    public AnthropicClient Create(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key required.", nameof(apiKey));
        // === SDK seam: adapt to the beta SDK's real constructor/options ===
        return new AnthropicClient(apiKey);
    }

    public AnthropicClient CreateForCurrentUser() =>
        Create(keys.GetApiKey() ?? throw new MissingApiKeyException());
}
```

### Classifier uses the factory (don't hold a client)

The existing classifier currently (presumably) holds one client built from the
company key. Change it to build a client **per call** via the factory. Keep the
rest of its tool-call / structured-extraction logic unchanged. **Match the real
method signature** — the names below (`BuildContext`, `ClassificationResult`,
`ClassifyAsync`) are from the planning conversation and may differ in the repo.

```csharp
public sealed class LlmClassifier(IClaudeClientFactory clients) : IClassifier
{
    public async Task<ClassificationResult> ClassifyAsync(
        BuildContext context, CancellationToken ct)
    {
        var client = clients.CreateForCurrentUser();   // bound to this user's key
        // ... existing tool-call / structured-extraction logic, unchanged ...
    }
}
```

Apply the same change to any other agent component that calls the SDK (e.g. an
`ISelector` implementation, if it uses the LLM).

### Optional key tester (used by settings UI)

```csharp
public readonly record struct KeyTestResult(bool Ok, string? Error);

public interface IClaudeKeyTester
{
    Task<KeyTestResult> TestAsync(string apiKey, CancellationToken ct = default);
}

public sealed class ClaudeKeyTester(IClaudeClientFactory clients) : IClaudeKeyTester
{
    public async Task<KeyTestResult> TestAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            var client = clients.Create(apiKey);
            // === SDK seam: smallest possible call to prove the key is live.
            // e.g. a 1-token message on Haiku. Adapt to the SDK's request type. ===
            _ = await client.Messages.CreateAsync(/* Haiku, max_tokens: 1, "hi" */, ct);
            return new KeyTestResult(true, null);
        }
        catch (Exception ex)   // NARROW this to the SDK's auth/HTTP exception
        {
            return new KeyTestResult(false, ex.Message);
        }
    }
}
```

### Settings component (Blazor)

```razor
@* ApiKeySettings.razor — class names are placeholders; style later *@
@using EdhDeckBuilder.Agent.Authentication
@inject SessionApiKeyProvider Keys
@inject IClaudeKeyTester Tester

<div class="api-key-settings">
    <h3>Connect your Anthropic account</h3>

    @if (_connected)
    {
        <p class="status-ok">✓ A key is connected for this session.</p>
        <button @onclick="Disconnect">Disconnect</button>
    }
    else
    {
        <p>
            Builds run on your own Anthropic API key, billed to your account.
            Create one at
            <a href="https://console.anthropic.com/settings/keys"
               target="_blank" rel="noopener">console.anthropic.com</a>,
            then paste it below. It's held only for this session.
        </p>

        <input type="password" placeholder="sk-ant-..." autocomplete="off"
               @bind="_keyInput" @bind:event="oninput" />

        <div class="actions">
            <button disabled="@_busy" @onclick="Connect">Connect</button>
            <button disabled="@(_busy || string.IsNullOrWhiteSpace(_keyInput))"
                    @onclick="Test">Test key</button>
        </div>

        @if (_message is { } msg)
        {
            <p class="@(_error ? "status-error" : "status-ok")">@msg</p>
        }
    }
</div>

@code {
    private string _keyInput = "";
    private bool _connected, _busy, _error;
    private string? _message;

    protected override void OnInitialized() => _connected = Keys.GetApiKey() is not null;

    private void Connect()
    {
        try
        {
            Keys.Set(_keyInput);     // format check happens here
            _keyInput = "";
            _connected = true;
            _error = false;
            _message = null;
        }
        catch (ArgumentException ex)
        {
            _error = true;
            _message = ex.Message;
        }
    }

    private async Task Test()
    {
        _busy = true; _message = null;
        var result = await Tester.TestAsync(_keyInput);
        _busy = false;
        _error = !result.Ok;
        _message = result.Ok ? "Key works." : $"Key rejected: {result.Error}";
    }

    private void Disconnect()
    {
        Keys.Clear();
        _connected = false;
        _message = null;
    }
}
```

### DI registration (adapt to existing setup)

```csharp
services.AddScoped<SessionApiKeyProvider>();
services.AddScoped<IClaudeApiKeyProvider>(sp =>
    sp.GetRequiredService<SessionApiKeyProvider>());      // same instance, both faces
services.AddScoped<IClaudeClientFactory, ClaudeClientFactory>();
services.AddScoped<IClaudeKeyTester, ClaudeKeyTester>();
services.AddScoped<IClassifier, LlmClassifier>();          // adapt to existing lifetime
```

Register the concrete `SessionApiKeyProvider` **and** map the interface to the
same instance, so the settings page (which calls `Set`) and the agent (which
reads via the interface) share one object per circuit.

## Security guardrails

- **Never log the key**, and never include it in error messages surfaced to other
  users or telemetry.
- **In-memory per session by default.** Don't persist it to disk in this design.
- **If you add "remember my key" later**, encrypt at rest with ASP.NET Core Data
  Protection scoped to the user — never store plaintext.
- **Handle HTTP 401** (rejected/expired/revoked key) at the agent boundary:
  catch it, call `Clear()`, and surface a "your key was rejected — please
  reconnect" state rather than a generic build failure. This is the most common
  BYOK runtime failure.

## Guardrails for Claude Code

- **The SDK calls are placeholders.** `AnthropicClient`, its constructor, and
  `Messages.CreateAsync` must be replaced with the beta SDK's real surface.
  Keep them confined to `ClaudeClientFactory` / `ClaudeKeyTester` so the seam
  stays in one place.
- **Match the real classifier/agent signatures.** `IClassifier`, `BuildContext`,
  `ClassificationResult` are from a conversation; verify against the repo and
  adapt. Convert *every* SDK-calling agent component to use the factory.
- **Remove the old company-key client construction** wherever it currently lives
  (config-bound singleton client / `ANTHROPIC_API_KEY` env read in the agent path).
- **Confirm hosting model** (Server vs WASM) before relying on the scoped-key
  approach; see the Hosting assumption section.
- **Don't relitigate settled architecture.** This change is purely about *where
  the key comes from* and *when the client is built* — the pipeline stays as is.

## Open questions / follow-ups

- Whether to gate the build button on `IClaudeApiKeyProvider.GetApiKey() != null`
  (likely yes — show "connect a key" instead of letting a build fail).
- Optional persistence with Data Protection if users find re-entering per session
  annoying.
- Surfacing approximate token/cost estimates per build, since users now pay
  directly and will care.
