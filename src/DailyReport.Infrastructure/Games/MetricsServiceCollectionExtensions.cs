using DailyReport.Core.Abstractions;
using DailyReport.Infrastructure.Games.Crosswords;
using DailyReport.Infrastructure.Games.MakeMeRed;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Infrastructure.Games;

public static class MetricsServiceCollectionExtensions
{
    /// <summary>One provider per <see cref="Core.Configuration.MetricsProvider"/>. Requires the games database and the GoatCounter client to be registered.</summary>
    public static IServiceCollection AddMetricsProviders(this IServiceCollection services)
    {
        services.AddSingleton<IGameMetricsProvider, CrosswordsMetricsProvider>();
        services.AddSingleton<IGameMetricsProvider, MakeMeRedMetricsProvider>();
        return services;
    }
}
