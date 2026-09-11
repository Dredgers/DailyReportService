using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.State;
using Microsoft.Extensions.Options;

namespace DailyReport.Worker.Hosting;

/// <summary>
/// Docker HEALTHCHECK entry point: healthy while the last successful run is under 26 hours old, or while the store
/// is younger than that and nothing has run yet. Unhealthy means a morning was missed and nobody noticed.
/// </summary>
public static class HealthCheck
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(26);

    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<ReportOptions>>().Value;
        var store = services.GetRequiredService<IRunStore>();
        var clock = services.GetRequiredService<TimeProvider>();

        var path = StateServiceCollectionExtensions.DatabasePath(options);
        if (!File.Exists(path))
        {
            // The scheduler creates the store the moment it starts; before that there is nothing to judge.
            Console.Out.WriteLine("healthy: state not created yet");
            return 0;
        }

        var lastSuccess = await store.LastSuccessAsync(cancellationToken);
        var reference = lastSuccess ?? File.GetCreationTimeUtc(path);
        var age = clock.GetUtcNow() - reference;

        var healthy = age <= MaxAge;
        var hours = age.TotalHours.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        Console.Out.WriteLine(healthy
            ? $"healthy: last success {hours} h ago"
            : $"unhealthy: no successful run for {hours} h");
        return healthy ? 0 : 1;
    }
}
