namespace EdhDeckBuilder.Agent.Authentication;

public sealed class ApiKeyRejectedException(Exception inner)
    : Exception("Your Anthropic API key was rejected. Please reconnect.", inner);
