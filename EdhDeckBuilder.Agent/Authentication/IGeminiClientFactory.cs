using EdhDeckBuilder.Agent.Llm.Gemini;

namespace EdhDeckBuilder.Agent.Authentication;

public interface IGeminiClientFactory
{
    GeminiRestClient CreateForCurrentUser();
    string SelectionModel { get; }
}
