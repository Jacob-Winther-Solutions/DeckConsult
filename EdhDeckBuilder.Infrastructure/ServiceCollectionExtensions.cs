using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Infrastructure.Edhrec;
using EdhDeckBuilder.Infrastructure.Scryfall;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EdhDeckBuilder.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ScryfallOptions>(configuration.GetSection("Scryfall"));
        services.AddHttpClient<ScryfallBulkClient>(c =>
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EdhDeckBuilder/1.0"));
        services.AddSingleton<CardRepository>();
        services.AddSingleton<ICardRepository>(sp => sp.GetRequiredService<CardRepository>());

        services.Configure<EdhrecOptions>(configuration.GetSection("Edhrec"));
        services.AddHttpClient<EdhrecClient>(c =>
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EdhDeckBuilder/1.0"));
        services.AddSingleton<SuggestionSource>();
        services.AddSingleton<ISuggestionSource>(sp => sp.GetRequiredService<SuggestionSource>());

        return services;
    }
}
