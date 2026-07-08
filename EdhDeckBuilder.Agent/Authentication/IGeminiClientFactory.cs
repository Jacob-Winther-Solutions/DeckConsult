using OpenAI.Chat;

namespace EdhDeckBuilder.Agent.Authentication;

public interface IGeminiClientFactory
{
    ChatClient CreateForCurrentUser();
    string SelectionModel { get; }
}
