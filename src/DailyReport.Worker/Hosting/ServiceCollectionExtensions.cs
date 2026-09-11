using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Data;
using DailyReport.Infrastructure.Delivery.Resend;
using DailyReport.Infrastructure.Games;
using DailyReport.Infrastructure.Probes;
using DailyReport.Infrastructure.Sources.GoatCounter;
using DailyReport.Infrastructure.State;
using Microsoft.Extensions.Options;

namespace DailyReport.Worker.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>Everything the report needs, in one place, so --once and the scheduler build the same graph.</summary>
    public static IServiceCollection AddDailyReport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReportOptions>()
            .Bind(configuration.GetSection(ReportOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ReportOptions>, ReportOptionsValidator>();

        // "Games" is a top-level array, so bind the root and let the wrapper pick the key up.
        services.AddOptions<GamesOptions>()
            .Bind(configuration)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GamesOptions>, GamesOptionsValidator>();

        services.AddSingleton(TimeProvider.System);

        services.AddGamesDatabase(configuration);
        services.AddGoatCounter(configuration);
        services.AddMetricsProviders();
        services.AddResend(configuration);
        services.AddReportState();
        services.AddHealthProbes(configuration);

        services.AddScoped<ReportRunner>();

        return services;
    }
}
