using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Discovery;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm;
using EdhDeckBuilder.Agent.Llm.Gemini;
using EdhDeckBuilder.Agent.Pipeline;
using EdhDeckBuilder.Agent.Prompts;
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
    /// The Anthropic API key is no longer read from configuration here. Each user supplies
    /// their own key via <see cref="SessionApiKeyProvider"/>, which is populated by the
    /// settings UI and lives in a Blazor Server circuit-scoped service.
    /// </remarks>
    public static IServiceCollection AddAgent(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Initialize logging from appsettings
        if (configuration != null)
        {
            var section = configuration.GetSection(InstrumentationOptions.Section);
            var options = new InstrumentationOptions();
            if (section.Exists())
            {
                if (bool.TryParse(section["LogClassificationResponses"], out var logClassEnabled))
                    options.LogClassificationResponses = logClassEnabled;
                if (bool.TryParse(section["EnableStructuredDeckBuildLogging"], out var logDeckEnabled))
                    options.EnableStructuredDeckBuildLogging = logDeckEnabled;
                if (bool.TryParse(section["EnableClassificationReasoning"], out var reasoningEnabled))
                    options.EnableClassificationReasoning = reasoningEnabled;
            }
            ClassificationResponseLogger.Initialize(options);
            ClassificationPrompt.SetInstrumentationOptions(options);
        }

        // Per-circuit key holder — registered twice so settings UI (concrete) and agent
        // (interface) share the same instance per circuit.
        services.AddScoped<SessionApiKeyProvider>();
        services.AddScoped<IClaudeApiKeyProvider>(sp => sp.GetRequiredService<SessionApiKeyProvider>());

        services.AddScoped<IClaudeClientFactory, ClaudeClientFactory>();
        services.AddScoped<IGeminiClientFactory, GeminiClientFactory>();
        services.AddScoped<IClaudeKeyTester, ClaudeKeyTester>();

        // ClassificationCache is global (cross-build, cross-circuit); LLM callers are scoped
        // because they depend on the per-circuit client factory.
        services.AddSingleton<ClassificationCache>();

        // Register both Anthropic and Gemini implementations
        services.AddScoped<LlmClassifier>();
        services.AddScoped<GeminiClassifier>();
        services.AddScoped<LlmSelector>();
        services.AddScoped<GeminiSelector>();
        services.AddScoped<LlmCommanderSelector>();
        services.AddScoped<GeminiCommanderSelector>();

        // Provider-aware dispatch via factory lambdas
        services.AddScoped<ILlmClassifier>(sp =>
            sp.GetRequiredService<IClaudeApiKeyProvider>().ActiveProvider == AiProvider.Google
                ? (ILlmClassifier)sp.GetRequiredService<GeminiClassifier>()
                : sp.GetRequiredService<LlmClassifier>());

        services.AddScoped<ICardSelector>(sp =>
            sp.GetRequiredService<IClaudeApiKeyProvider>().ActiveProvider == AiProvider.Google
                ? (ICardSelector)sp.GetRequiredService<GeminiSelector>()
                : sp.GetRequiredService<LlmSelector>());

        services.AddScoped<ICommanderSelector>(sp =>
            sp.GetRequiredService<IClaudeApiKeyProvider>().ActiveProvider == AiProvider.Google
                ? (ICommanderSelector)sp.GetRequiredService<GeminiCommanderSelector>()
                : sp.GetRequiredService<LlmCommanderSelector>());

        services.AddScoped<IDeckBuilder, DeckBuilder>();

        // Commander discovery
        services.AddScoped<ICommanderDiscovery, CommanderDiscovery>();

        return services;
    }
}
