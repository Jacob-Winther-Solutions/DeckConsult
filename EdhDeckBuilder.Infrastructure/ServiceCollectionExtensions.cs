using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Infrastructure.Edhrec;
using EdhDeckBuilder.Infrastructure.Scryfall;
using EdhDeckBuilder.Infrastructure.Spellbook;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ScryfallOptions>(configuration.GetSection("Scryfall"));
        services.AddHttpClient<ScryfallBulkClient>(c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EdhDeckBuilder/1.0");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddTransient<ICardRefreshService, CardRefreshService>();
        services.AddSingleton<IPartnershipEligibilityRule, PartnershipEligibilityRule>();

        services.Configure<EdhrecOptions>(configuration.GetSection("Edhrec"));
        services.AddHttpClient<EdhrecClient>(c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EdhDeckBuilder/1.0");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddSingleton<IEdhrecClient>(sp => sp.GetRequiredService<EdhrecClient>());

        services.AddSingleton<CardRepository>(sp => new CardRepository(
            sp.GetRequiredService<ScryfallBulkClient>(),
            sp.GetRequiredService<IEdhrecClient>(),
            sp.GetRequiredService<ILogger<CardRepository>>()));
        services.AddSingleton<ICardRepository>(sp => sp.GetRequiredService<CardRepository>());
        services.AddSingleton<SuggestionSource>(sp => new SuggestionSource(
            sp.GetRequiredService<EdhrecClient>(),
            sp.GetRequiredService<ICardRepository>(),
            sp.GetRequiredService<ILogger<SuggestionSource>>()));
        services.AddSingleton<ISuggestionSource>(sp => sp.GetRequiredService<SuggestionSource>());
        services.AddSingleton<IPartnerPairingRepository>(sp => new PartnerPairingRepository(
            sp.GetRequiredService<SuggestionSource>()));

        services.AddHttpClient<CommanderSpellbookClient>(c =>
        {
            c.BaseAddress = new Uri("https://backend.commanderspellbook.com/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EdhDeckBuilder/1.0");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IComboSource>(sp => sp.GetRequiredService<CommanderSpellbookClient>());
        services.AddSingleton<ComboPoolSource>(sp => new ComboPoolSource(
            sp.GetRequiredService<IComboSource>(),
            sp.GetRequiredService<ICardRepository>(),
            sp.GetRequiredService<ILogger<ComboPoolSource>>()));
        services.AddSingleton<IComboCardSource>(sp => sp.GetRequiredService<ComboPoolSource>());

        return services;
    }
}
