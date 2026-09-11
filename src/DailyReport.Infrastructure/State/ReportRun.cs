namespace DailyReport.Infrastructure.State;

public enum RunStatus
{
    Sent,
    DryRun,
    Failed,
}

/// <summary>One attempt to produce a report. This is the service's own memory: idempotency, catch-up, and the health check read it.</summary>
public sealed class ReportRun
{
    public long Id { get; set; }

    /// <summary>yyyy-MM-dd of the report date D.</summary>
    public string ReportDate { get; set; } = "";

    /// <summary>"schedule", "catch-up", "once".</summary>
    public string Trigger { get; set; } = "";

    public long StartedAtUnixMs { get; set; }

    public long FinishedAtUnixMs { get; set; }

    public RunStatus Status { get; set; }

    public string? Subject { get; set; }

    public int RedCount { get; set; }

    public int WarnCount { get; set; }

    public string? ProviderMessageId { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset StartedAt => DateTimeOffset.FromUnixTimeMilliseconds(StartedAtUnixMs);

    public DateTimeOffset FinishedAt => DateTimeOffset.FromUnixTimeMilliseconds(FinishedAtUnixMs);
}
