using DailyReport.Core.Model;
using DailyReport.Core.Time;

namespace DailyReport.Core.Composition;

/// <summary>What a provider hands the composer for one game.</summary>
public sealed record GameInput(string GameKey, string Name, ReportWindow Window, IReadOnlyList<DailySeries> Series);

/// <summary>
/// Turns raw daily series into metrics with baselines, and orders health so the bad news is first.
/// Deltas are computed here, once, never in a renderer.
/// </summary>
public static class ReportComposer
{
    public static Report Compose(
        DateOnly reportDate,
        DateTimeOffset generatedAt,
        IReadOnlyList<GameInput> games,
        IReadOnlyList<CheckResult> checks,
        IReadOnlyList<SourceFailure> failures)
    {
        var sections = games
            .Select(g => new GameSection(g.GameKey, g.Name, g.Window.GameDay, g.Window.Zone.Id, g.Series.Select(s => ToMetric(s, g.Window)).ToList()))
            .ToList();

        var orderedChecks = checks
            .OrderBy(c => Rank(c.Status))
            .ThenBy(c => c.GameKey, StringComparer.Ordinal)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

        return new Report(reportDate, generatedAt, orderedChecks, sections, failures);
    }

    public static Metric ToMetric(DailySeries series, ReportWindow window)
    {
        var baseline = window.BaselineDays.Select(series.On).Where(v => v is not null).Select(v => v!.Value).ToList();

        return new Metric(
            series.Definition,
            Value: series.On(window.GameDay),
            DayBefore: series.On(window.DayBefore),
            SevenDayMean: baseline.Count == 0 ? null : baseline.Average(),
            BaselineDaysPresent: baseline.Count,
            SameWeekdayLastWeek: series.On(window.SameWeekdayLastWeek));
    }

    private static int Rank(CheckStatus status) => status switch
    {
        CheckStatus.Fail => 0,
        CheckStatus.Error => 1,
        CheckStatus.Warn => 2,
        CheckStatus.Pass => 3,
        CheckStatus.Skipped => 4,
        _ => 5,
    };
}
