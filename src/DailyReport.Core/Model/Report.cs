namespace DailyReport.Core.Model;

public enum ReportStatus
{
    Green,
    Amber,
    Red,
}

/// <summary>One game's part of the report. <see cref="ZoneId"/> is shown so the reader knows which "yesterday" this is.</summary>
public sealed record GameSection(string GameKey, string Name, DateOnly GameDay, string ZoneId, IReadOnlyList<Metric> Metrics);

public sealed record Report(
    DateOnly ReportDate,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<CheckResult> Health,
    IReadOnlyList<GameSection> Games,
    IReadOnlyList<SourceFailure> Failures)
{
    public int RedCount => Health.Count(c => c.IsRed) + Failures.Count;

    public int WarnCount => Health.Count(c => c.Status == CheckStatus.Warn);

    public ReportStatus Status =>
        RedCount > 0 ? ReportStatus.Red :
        WarnCount > 0 ? ReportStatus.Amber :
        ReportStatus.Green;
}
