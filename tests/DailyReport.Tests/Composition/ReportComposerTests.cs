using DailyReport.Core.Composition;
using DailyReport.Core.Model;
using DailyReport.Core.Time;

namespace DailyReport.Tests.Composition;

public sealed class ReportComposerTests
{
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), TimeZoneInfo.Utc);

    private static readonly MetricDefinition Arrivals = new("arrivals", "Traffic", "Arrivals", Lane.All, MetricUnit.Count);

    private static DailySeries Series(params (int daysAgo, double? value)[] points)
    {
        var byDay = new Dictionary<DateOnly, double?>();
        foreach (var (daysAgo, value) in points)
        {
            byDay[Window.GameDay.AddDays(-daysAgo)] = value;
        }

        return new DailySeries(Arrivals, byDay);
    }

    [Test]
    public void Metric_carries_value_day_before_seven_day_mean_and_same_weekday()
    {
        // Game day = 100; day before = 80; seven days before that = 70,70,70,70,70,70,80 (mean 71.43); same weekday last week = 70.
        var series = Series((0, 100), (1, 80), (2, 70), (3, 70), (4, 70), (5, 70), (6, 70), (7, 70));

        var m = ReportComposer.ToMetric(series, Window);

        Assert.Multiple(() =>
        {
            Assert.That(m.Value, Is.EqualTo(100));
            Assert.That(m.DayBefore, Is.EqualTo(80));
            Assert.That(m.SevenDayMean, Is.EqualTo((70 * 6 + 80) / 7.0).Within(1e-9));
            Assert.That(m.BaselineDaysPresent, Is.EqualTo(7));
            Assert.That(m.SameWeekdayLastWeek, Is.EqualTo(70));
            Assert.That(m.VsDayBefore, Is.EqualTo(new Delta(20, 25)));
            Assert.That(m.VsSameWeekdayLastWeek!.Value.Percent, Is.EqualTo(100.0 * 30 / 70).Within(1e-9));
        });
    }

    [Test]
    public void Missing_days_are_not_zero()
    {
        var series = Series((0, 5), (2, 10), (4, 20));

        var m = ReportComposer.ToMetric(series, Window);

        Assert.Multiple(() =>
        {
            Assert.That(m.DayBefore, Is.Null);
            Assert.That(m.VsDayBefore, Is.Null);
            Assert.That(m.SevenDayMean, Is.EqualTo(15));
            Assert.That(m.BaselineDaysPresent, Is.EqualTo(2));
            Assert.That(m.SameWeekdayLastWeek, Is.Null);
        });
    }

    [Test]
    public void Percent_delta_is_null_against_a_zero_baseline()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Delta.Between(3, 0), Is.EqualTo(new Delta(3, null)));
            Assert.That(Delta.Between(0, 0), Is.EqualTo(new Delta(0, null)));
            Assert.That(Delta.Between(null, 4), Is.Null);
        });
    }

    [Test]
    public void Health_sorts_bad_news_first_then_by_game_and_name()
    {
        var checks = new[]
        {
            CheckResult.Pass("makemered", "Healthz", "200", TimeSpan.Zero),
            CheckResult.Skipped("makemered", "WebSocket", "no socket"),
            CheckResult.Warn("crosswords", "Puzzle queue", "2 left", TimeSpan.Zero),
            CheckResult.Fail("crosswords", "Puzzle published", "stale", TimeSpan.Zero),
            CheckResult.Error("makemered", "Puzzle published", "timeout", TimeSpan.Zero),
            CheckResult.Pass("crosswords", "Healthz", "200", TimeSpan.Zero),
        };

        var report = ReportComposer.Compose(Window.ReportDate, DateTimeOffset.UnixEpoch, [], checks, []);

        Assert.Multiple(() =>
        {
            Assert.That(report.Health.Select(c => $"{c.Status}:{c.GameKey}:{c.Name}"), Is.EqualTo(new[]
            {
                "Fail:crosswords:Puzzle published",
                "Error:makemered:Puzzle published",
                "Warn:crosswords:Puzzle queue",
                "Pass:crosswords:Healthz",
                "Pass:makemered:Healthz",
                "Skipped:makemered:WebSocket",
            }));
            Assert.That(report.RedCount, Is.EqualTo(2));
            Assert.That(report.WarnCount, Is.EqualTo(1));
            Assert.That(report.Status, Is.EqualTo(ReportStatus.Red));
        });
    }

    [Test]
    public void A_source_failure_makes_the_report_red_even_with_green_health()
    {
        var report = ReportComposer.Compose(
            Window.ReportDate,
            DateTimeOffset.UnixEpoch,
            [],
            [CheckResult.Pass("crosswords", "Healthz", "200", TimeSpan.Zero)],
            [new SourceFailure("crosswords", "Metrics", "connection refused")]);

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ReportStatus.Red));
            Assert.That(report.RedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Warnings_alone_make_it_amber_and_nothing_makes_it_green()
    {
        var amber = ReportComposer.Compose(Window.ReportDate, DateTimeOffset.UnixEpoch, [], [CheckResult.Warn("crosswords", "Puzzle queue", "2", TimeSpan.Zero)], []);
        var green = ReportComposer.Compose(Window.ReportDate, DateTimeOffset.UnixEpoch, [], [CheckResult.Pass("crosswords", "Healthz", "200", TimeSpan.Zero)], []);

        Assert.Multiple(() =>
        {
            Assert.That(amber.Status, Is.EqualTo(ReportStatus.Amber));
            Assert.That(green.Status, Is.EqualTo(ReportStatus.Green));
        });
    }

    [Test]
    public void Sections_carry_the_game_day_and_zone_for_the_footer()
    {
        var copenhagen = new ReportWindow(Window.ReportDate, TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"));
        var input = new GameInput("crosswords", "Competitive Crosswords", copenhagen, [Series((0, 1))]);

        var report = ReportComposer.Compose(Window.ReportDate, DateTimeOffset.UnixEpoch, [input], [], []);

        Assert.Multiple(() =>
        {
            Assert.That(report.Games[0].GameDay, Is.EqualTo(new DateOnly(2026, 9, 10)));
            Assert.That(report.Games[0].ZoneId, Is.EqualTo("Europe/Copenhagen"));
            Assert.That(report.Games[0].Metrics[0].Value, Is.EqualTo(1));
        });
    }
}
