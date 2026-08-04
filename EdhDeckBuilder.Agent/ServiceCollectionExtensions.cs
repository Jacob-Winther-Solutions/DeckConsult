using EdhDeckBuilder.Agent.Analysis;
using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Authentication.Claude;
using EdhDeckBuilder.Agent.Authentication.Gemini;
using EdhDeckBuilder.Agent.Authentication.OpenAI;
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
                if (bool.TryParse(section["LogRawLlmRequests"], out var logRawEnabled))
                    options.LogRawLlmRequests = logRawEnabled;
            }
            ClassificationResponseLogger.Initialize(options);
            ClassificationPrompt.SetInstrumentationOptions(options);
            InstrumentationOptions.SetCurrent(options);
        }

        // Per-circuit key holder — registered twice so settings UI (concrete) and agent
        // (interface) share the same instance per circuit.
        services.AddScoped<SessionApiKeyProvider>();
        services.AddScoped<IClaudeApiKeyProvider>(sp => sp.GetRequiredService<SessionApiKeyProvider>());

        // Named HttpClients for KeyTester probes
        services.AddHttpClient("claude", c => c.Timeout = TimeSpan.FromSeconds(120));
        services.AddHttpClient("openai", c => c.Timeout = TimeSpan.FromSeconds(120));

        // ClaudeHttpLlmClientFactory — typed HttpClient so connection pooling is managed by
        // IHttpClientFactory rather than creating a raw HttpClient per circuit.
        services.AddHttpClient<ClaudeHttpLlmClientFactory>(c => c.Timeout = TimeSpan.FromSeconds(120));

        // GeminiClientFactory needs a pooled HttpClient — same pattern as above.
        services.AddHttpClient<GeminiClientFactory>(c => c.Timeout = TimeSpan.FromSeconds(120));
        services.AddScoped<IGeminiClientFactory>(sp => sp.GetRequiredService<GeminiClientFactory>());

        // Per-circuit pacing state so each user's key gets its own RPM budget.
        services.AddScoped<GeminiRateLimiter>();

        // GeminiLlmClientFactory wraps IGeminiClientFactory behind ILlmClientFactory.
        services.AddScoped<GeminiLlmClientFactory>();

        // OpenAiLlmClientFactory — typed HttpClient so connection pooling is managed by IHttpClientFactory.
        services.AddHttpClient<OpenAiLlmClientFactory>(c => c.Timeout = TimeSpan.FromSeconds(120));
        services.AddScoped<OpenAiLlmClientFactory>();

        // Provider-dispatched ILlmClientFactory — resolved once per Blazor circuit.
        services.AddScoped<ILlmClientFactory>(sp =>
        {
            var keys = sp.GetRequiredService<IClaudeApiKeyProvider>();
            return keys.ActiveProvider switch
            {
                AiProvider.Google => (ILlmClientFactory)sp.GetRequiredService<GeminiLlmClientFactory>(),
                AiProvider.OpenAI => sp.GetRequiredService<OpenAiLlmClientFactory>(),
                _                 => sp.GetRequiredService<ClaudeHttpLlmClientFactory>(),
            };
        });

        services.AddScoped<IKeyTester, KeyTester>();

        // ClassificationCache is global (cross-build, cross-circuit); LLM callers are scoped
        // because they depend on the per-circuit client factory.
        services.AddSingleton<ClassificationCache>();

        // Three unified adapters replace the former six provider-specific classes.
        services.AddScoped<ILlmClassifier, LlmClassifier>();
        services.AddScoped<ICardSelector, LlmSelector>();
        services.AddScoped<ICommanderSelector, LlmCommanderSelector>();

        services.AddScoped<IDeckBuilder, DeckBuilder>();
        services.AddScoped<ILockedCardValidator, LockedCardValidator>();
        services.AddScoped<IDeckAnalyzer, DeckAnalyzer>();
        services.AddScoped<IDeckUpgrader, DeckUpgrader>();
        services.AddScoped<IComboFinder, ComboFinder>();
        services.AddSingleton<DecklistParser>();

        // Commander discovery
        services.AddScoped<ICommanderDiscovery, CommanderDiscovery>();

        return services;
    }
}
