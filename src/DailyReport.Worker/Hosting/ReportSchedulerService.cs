using DailyReport.Core.Configuration;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.State;
using Microsoft.Extensions.Options;

namespace DailyReport.Worker.Hosting;

/// <summary>
/// Runs the report at Report:SendAtLocalTime every day in Report:TimeZone. On start, if today's slot has passed
/// and today's report was never sent (the container was down at 07:00), it runs immediately. One run per report
/// date: the runner refuses to send twice.
/// </summary>
public sealed class ReportSchedulerService(
    IServiceScopeFactory scopes,
    IOptions<ReportOptions> reportOptions,
    IRunStore runStore,
    TimeProvider clock,
    ILogger<ReportSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = reportOptions.Value;
        var zone = options.Zone;
        var sendAt = options.SendAt;

        var lastSent = await runStore.LastSentReportDateAsync(stoppingToken);
        if (ReportSchedule.IsCatchUpDue(clock.GetUtcNow(), sendAt, zone, lastSent))
        {
            var today = ReportSchedule.ReportDateAt(clock.GetUtcNow(), zone);
            logger.LogWarning("Today's {SendAt} {Zone} slot has passed and {ReportDate} was not sent; catching up now", sendAt, zone.Id, today);
            await RunSafelyAsync(today, "catch-up", stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            var next = ReportSchedule.NextRunUtc(now, sendAt, zone);
            logger.LogInformation("Next report at {Next:u} ({Local} {Zone})", next, TimeZoneInfo.ConvertTime(next, zone).ToString("yyyy-MM-dd HH:mm"), zone.Id);

            try
            {
                await Task.Delay(next - now, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSafelyAsync(ReportSchedule.ReportDateAt(next, zone), "schedule", stoppingToken);
        }
    }

    private async Task RunSafelyAsync(DateOnly reportDate, string trigger, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<ReportRunner>();
            var result = await runner.RunAsync(reportDate, dryRun: false, force: false, trigger, cancellationToken);
            logger.LogInformation("Run for {ReportDate:yyyy-MM-dd} finished: {Outcome}", reportDate, result.Outcome);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The runner already turned every expected failure into a red line or a Failed run; this is the unexpected kind.
            logger.LogError(ex, "Run for {ReportDate} crashed", reportDate);
        }
    }
}
