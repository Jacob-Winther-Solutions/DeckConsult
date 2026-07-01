using Microsoft.Extensions.Configuration;

namespace EdhDeckBuilder.Agent.Authentication;

/// <summary>
/// Per-circuit in-memory holder for the user's Anthropic API key. Register as Scoped.
/// The settings component calls <see cref="Set"/> and <see cref="Clear"/>; the agent
/// reads through <see cref="IClaudeApiKeyProvider"/>.
/// </summary>
/// <remarks>
/// If <c>Anthropic:ApiKey</c> is present in configuration (user-secrets or environment
/// variable), the key is pre-populated so the app works in development without the UI
/// connect flow. In production this should be left unset — each user supplies their own key.
/// </remarks>
public sealed class SessionApiKeyProvider : IClaudeApiKeyProvider
{
    private string? _key;

    public SessionApiKeyProvider(IConfiguration config)
    {
        var configKey = config["Anthropic:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configKey))
            _key = configKey.Trim();
    }

    public string? GetApiKey() => _key;

    public string SelectedModel { get; set; } = ClaudeModels.Haiku;

    /// <summary>Sets the API key after validating the sk-ant- prefix.</summary>
    public void Set(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) ||
            !apiKey.Trim().StartsWith("sk-ant-", StringComparison.Ordinal))
            throw new ArgumentException(
                "That doesn't look like an Anthropic API key (expected 'sk-ant-' prefix).",
                nameof(apiKey));
        _key = apiKey.Trim();
    }

    public void Clear() => _key = null;
}
