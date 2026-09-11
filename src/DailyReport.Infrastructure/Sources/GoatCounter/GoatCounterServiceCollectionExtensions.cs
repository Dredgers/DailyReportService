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
    private static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddGoatCounter(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GoatCounterClientOptions>()
            .Bind(configuration.GetSection(GoatCounterClientOptions.SectionName));

        // Environment-backed by default; tests substitute a fake before resolving the client.
        services.TryAddSingleton<ISecretReader, EnvironmentSecretReader>();

        services.AddHttpClient(GoatCounterClient.HttpClientName, client => client.Timeout = HttpClientTimeout)
            .AddStandardResilienceHandler();

        services.TryAddSingleton<IGoatCounterClient, GoatCounterClient>();

        return services;
    }
}
