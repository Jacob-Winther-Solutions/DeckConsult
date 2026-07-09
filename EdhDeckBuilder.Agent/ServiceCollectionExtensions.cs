using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Authentication.Claude;
using EdhDeckBuilder.Agent.Authentication.Gemini;
using EdhDeckBuilder.Agent.Discovery;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm;
using EdhDeckBuilder.Agent.Llm.Claude;
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

        // GeminiClientFactory needs a pooled HttpClient — register as a typed client so
        // IHttpClientFactory manages lifetime. AddHttpClient defaults to Transient; we
        // then re-expose it as Scoped via the interface so it composes with the other
        // per-circuit services.
        services.AddHttpClient<GeminiClientFactory>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddScoped<IGeminiClientFactory>(sp => sp.GetRequiredService<GeminiClientFactory>());

        // Per-circuit pacing state so each user's key gets its own RPM budget rather than
        // sharing one throttle across the whole deployment.
        services.AddScoped<GeminiRateLimiter>();

        services.AddScoped<IKeyTester, KeyTester>();

        // ClassificationCache is global (cross-build, cross-circuit); LLM callers are scoped
        // because they depend on the per-circuit client factory.
        services.AddSingleton<ClassificationCache>();

        // Register both Claude and Gemini implementations
        services.AddScoped<ClaudeClassifier>();
        services.AddScoped<GeminiClassifier>();
        services.AddScoped<ClaudeSelector>();
        services.AddScoped<GeminiSelector>();
        services.AddScoped<ClaudeCommanderSelector>();
        services.AddScoped<GeminiCommanderSelector>();

        // Provider-aware dispatch via factory lambdas
        services.AddScoped<ILlmClassifier>(sp =>
            sp.GetRequiredService<IClaudeApiKeyProvider>().ActiveProvider == AiProvider.Google
                ? (ILlmClassifier)sp.GetRequiredService<GeminiClassifier>()
                : sp.GetRequiredService<ClaudeClassifier>());

        services.AddScoped<ICardSelector>(sp =>
            sp.GetRequiredService<IClaudeApiKeyProvider>().ActiveProvider == AiProvider.Google
                ? (ICardSelector)sp.GetRequiredService<GeminiSelector>()
                : sp.GetRequiredService<ClaudeSelector>());

        services.AddScoped<ICommanderSelector>(sp =>
            sp.GetRequiredService<IClaudeApiKeyProvider>().ActiveProvider == AiProvider.Google
                ? (ICommanderSelector)sp.GetRequiredService<GeminiCommanderSelector>()
                : sp.GetRequiredService<ClaudeCommanderSelector>());

        services.AddScoped<IDeckBuilder, DeckBuilder>();

        // Commander discovery
        services.AddScoped<ICommanderDiscovery, CommanderDiscovery>();

        return services;
    }
}
