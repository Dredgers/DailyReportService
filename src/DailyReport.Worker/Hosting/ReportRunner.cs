using DailyReport.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DailyReport.Worker.Hosting;

/// <summary>
/// Runs one report. R1 skeleton: proves configuration binds and validates, and prints what it would report on.
/// R9 replaces the body with collect → compose → render → send → record.
/// </summary>
public sealed class ReportRunner(
    IOptions<ReportOptions> report,
    IOptions<GamesOptions> games,
    TimeProvider clock,
    ILogger<ReportRunner> logger)
{
    public Task<int> RunOnceAsync(Invocation invocation, CancellationToken cancellationToken)
    {
        var r = report.Value;
        var reportDate = invocation.Date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), r.Zone).DateTime);

        logger.LogInformation("Daily report skeleton for {ReportDate} ({Zone}); dry-run={DryRun}", reportDate, r.TimeZone, invocation.DryRun);

        Console.Out.WriteLine($"Daily report {reportDate:yyyy-MM-dd} · skeleton");
        foreach (var g in games.Value.Games)
        {
            var yesterday = reportDate.AddDays(-1);
            Console.Out.WriteLine($"  {g.Name} ({g.Key}) · {g.BaseUrl} · game day {yesterday:yyyy-MM-dd} {g.DayTimeZone} · metrics {g.Metrics.Provider}/{g.Metrics.Arrivals}");
        }

        return Task.FromResult(0);
    }
}
