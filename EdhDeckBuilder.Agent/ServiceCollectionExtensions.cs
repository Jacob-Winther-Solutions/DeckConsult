using Anthropic;
using Anthropic.Core;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm;
using EdhDeckBuilder.Agent.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EdhDeckBuilder.Agent;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Agent-layer services. Call after <c>AddInfrastructure</c> because
    /// <see cref="IDeckBuilder"/> depends on <c>ISuggestionSource</c> from Infrastructure.
    /// </summary>
    /// <remarks>
    /// The Anthropic API key is read from <c>Anthropic:ApiKey</c> in configuration (user-secrets
    /// or <c>appsettings.json</c>). If absent, the SDK falls back to the <c>ANTHROPIC_API_KEY</c>
    /// environment variable automatically.
    /// </remarks>
    public static IServiceCollection AddAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AnthropicClient>(_ =>
        {
            var apiKey = configuration["Anthropic:ApiKey"];
            return string.IsNullOrWhiteSpace(apiKey)
                ? new AnthropicClient()                             // reads ANTHROPIC_API_KEY env var
                : new AnthropicClient(new ClientOptions { ApiKey = apiKey });
        });

        services.AddSingleton<ClassificationCache>();
        services.AddSingleton<ILlmClassifier, LlmClassifier>();
        services.AddSingleton<ICardSelector,  LlmSelector>();
        services.AddSingleton<IDeckBuilder,   DeckBuilder>();

        return services;
    }
}
