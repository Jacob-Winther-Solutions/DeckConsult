namespace EdhDeckBuilder.Agent.Authentication;

public interface IClaudeApiKeyProvider
{
    string? GetApiKey();
    string SelectedModel { get; }
    AiProvider ActiveProvider { get; }
}
