namespace DailyReport.Worker.Hosting;

/// <summary>R1 placeholder. R9 gives this a Cronos schedule, catch-up on start, and one-run-per-date idempotency.</summary>
public sealed class ReportSchedulerService(ILogger<ReportSchedulerService> logger, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogWarning("Scheduler not implemented yet (R9). Use --once for now. Stopping.");
        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}
