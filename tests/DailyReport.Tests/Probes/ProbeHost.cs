using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Probes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DailyReport.Tests.Probes;

/// <summary>
/// Builds the same DI graph <see cref="ProbeServiceCollectionExtensions.AddHealthProbes"/> wires up for the
/// Worker, with a one-second probe timeout so a hung or slow stub fails a test quickly instead of the default
/// 15 seconds — exactly the override the job notes call for tests to apply themselves.
/// </summary>
internal static class ProbeHost
{
    public static ServiceProvider Build(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddHealthProbes(configuration ?? new ConfigurationBuilder().Build());
        services.Configure<ReportOptions>(o => o.ProbeTimeoutSeconds = 1);
        return services.BuildServiceProvider();
    }
}
