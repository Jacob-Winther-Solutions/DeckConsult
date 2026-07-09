using EdhDeckBuilder.Agent.Authentication;

namespace EdhDeckBuilder.Agent.Interfaces;

public interface IClaudeApiKeyProvider
{
    string? GetApiKey();
    string SelectedModel { get; }
    AiProvider ActiveProvider { get; }
}
