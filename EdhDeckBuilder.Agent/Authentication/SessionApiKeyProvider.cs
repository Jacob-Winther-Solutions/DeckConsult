using EdhDeckBuilder.Agent.Authentication.Claude;
using EdhDeckBuilder.Agent.Interfaces;
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
    private string? _anthropicKey;
    private string? _gitHubKey;
    private string? _googleKey;
    private AiProvider _activeProvider;

    public SessionApiKeyProvider(IConfiguration config)
    {
        var anthropicKey = config["Anthropic:ApiKey"];
        if (!string.IsNullOrWhiteSpace(anthropicKey))
            _anthropicKey = anthropicKey.Trim();

        var gitHubKey = config["GitHub:ApiKey"];
        if (!string.IsNullOrWhiteSpace(gitHubKey))
            _gitHubKey = gitHubKey.Trim();

        var googleKey = config["Google:ApiKey"];
        if (!string.IsNullOrWhiteSpace(googleKey))
            _googleKey = googleKey.Trim();

        // Read initial provider preference from config, default to Anthropic
        var providerSetting = config["Provider:Default"];
        _activeProvider = providerSetting switch
        {
            string s when s.Equals("Google", StringComparison.OrdinalIgnoreCase) => AiProvider.Google,
            string s when s.Equals("GitHubModels", StringComparison.OrdinalIgnoreCase) => AiProvider.GitHubModels,
            _ => AiProvider.Anthropic,
        };
    }

    public AiProvider ActiveProvider
    {
        get => _activeProvider;
        set => _activeProvider = value;
    }

    public string? GetApiKey() =>
        ActiveProvider switch
        {
            AiProvider.Google => _googleKey,
            AiProvider.GitHubModels => _gitHubKey,
            _ => _anthropicKey,
        };

    public string SelectedModel { get; set; } = ClaudeModels.Haiku;

    /// <summary>Sets the API key after validating the prefix for the active provider.</summary>
    public void Set(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be empty.", nameof(apiKey));

        var trimmed = apiKey.Trim();

        if (ActiveProvider == AiProvider.Google)
        {
            // Google API keys can start with AIza, AQ, or other prefixes
            if (trimmed.Length < 20)
                throw new ArgumentException(
                    "That doesn't look like a valid Google API key (too short).",
                    nameof(apiKey));
            _googleKey = trimmed;
        }
        else if (ActiveProvider == AiProvider.GitHubModels)
        {
            if (!trimmed.StartsWith("ghp_", StringComparison.Ordinal) &&
                !trimmed.StartsWith("github_pat_", StringComparison.Ordinal))
                throw new ArgumentException(
                    "That doesn't look like a GitHub Personal Access Token (expected 'ghp_' or 'github_pat_' prefix).",
                    nameof(apiKey));
            _gitHubKey = trimmed;
        }
        else
        {
            if (!trimmed.StartsWith("sk-ant-", StringComparison.Ordinal))
                throw new ArgumentException(
                    "That doesn't look like an Anthropic API key (expected 'sk-ant-' prefix).",
                    nameof(apiKey));
            _anthropicKey = trimmed;
        }
    }

    public void Clear()
    {
        if (ActiveProvider == AiProvider.Google)
            _googleKey = null;
        else if (ActiveProvider == AiProvider.GitHubModels)
            _gitHubKey = null;
        else
            _anthropicKey = null;
    }
}
