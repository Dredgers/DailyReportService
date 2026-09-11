using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DailyReport.Infrastructure.Sources.GoatCounter;

/// <summary>Wires up <see cref="IGoatCounterClient"/>. Called from the Worker's composition root (R9); safe to
/// call even for a run with no GoatCounter-backed game configured, since nothing here touches the network.</summary>
public static class GoatCounterServiceCollectionExtensions
{
    public static IServiceCollection AddGoatCounter(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GoatCounterClientOptions>()
            .Bind(configuration.GetSection(GoatCounterClientOptions.SectionName));

        // Environment-backed by default; tests substitute a fake before resolving the client.
        services.TryAddSingleton<ISecretReader, EnvironmentSecretReader>();

        // No HttpClient.Timeout: the standard resilience handler's total timeout (30 s) is the only clock, so a stall
        // surfaces as Polly's TimeoutRejectedException, which the providers report as a section failure.
        services.AddHttpClient(GoatCounterClient.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DailyReportService/1.0 (+https://github.com/Dredgers/DailyReportService)");
            })
            .AddStandardResilienceHandler();

        services.TryAddSingleton<IGoatCounterClient, GoatCounterClient>();

        return services;
    }
}
