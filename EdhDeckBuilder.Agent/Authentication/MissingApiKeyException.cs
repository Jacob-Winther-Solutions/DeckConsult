namespace EdhDeckBuilder.Agent.Authentication;

public sealed class MissingApiKeyException()
    : Exception("No Anthropic API key is connected for this session. Please connect your key to continue.");
