using EdhDeckBuilder.Agent.Llm.Gemini;

namespace EdhDeckBuilder.Agent.Interfaces;

public interface IGeminiClientFactory
{
    GeminiRestClient CreateForCurrentUser();
    string SelectionModel { get; }
}
