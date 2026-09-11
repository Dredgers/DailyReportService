using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Infrastructure.Probes;

public static class ProbeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the four Phase 1 probes as <see cref="IHealthProbe"/> so a consumer (R9's runner) can inject
    /// <see cref="IEnumerable{T}"/> of them and fan out over every configured game. Each probe's own timeout
    /// comes from <see cref="ReportOptions"/>, already registered by the Worker; the "probes" HTTP client has
    /// no client-level timeout on purpose, so the linked cancellation token in <see cref="ProbeBase"/> is the
    /// only thing that ever cuts a request off.
    /// </summary>
    public static IServiceCollection AddHealthProbes(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PuzzleSourceOptions>()
            .Bind(configuration.GetSection(PuzzleSourceOptions.SectionName));

        services.AddHttpClient("probes", c =>
        {
            c.Timeout = Timeout.InfiniteTimeSpan;
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DailyReportService/1.0 (+https://github.com/Dredgers/DailyReportService)");
        });

        services.AddSingleton<IHealthProbe, HealthzProbe>();
        services.AddSingleton<IHealthProbe, CrosswordsPuzzlePublishedProbe>();
        services.AddSingleton<IHealthProbe, MakeMeRedPuzzlePublishedProbe>();
        services.AddSingleton<IHealthProbe, PuzzleQueueProbe>();

        return services;
    }
}
