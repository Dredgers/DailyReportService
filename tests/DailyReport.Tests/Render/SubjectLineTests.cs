using DailyReport.Core.Model;
using DailyReport.Core.Render;

namespace DailyReport.Tests.Render;

public sealed class SubjectLineTests
{
    private static readonly DateOnly ReportDate = new(2026, 9, 11);
    private static readonly DateTimeOffset GeneratedAt = new(2026, 9, 11, 5, 0, 0, TimeSpan.Zero);

    private static Report Report(IReadOnlyList<CheckResult> checks, IReadOnlyList<SourceFailure> failures) =>
        new(ReportDate, GeneratedAt, checks, [], failures);

    [Test]
    public void All_green_reads_all_green()
    {
        var report = Report([CheckResult.Pass("crosswords", "Healthz", "200 OK", TimeSpan.Zero)], []);

        Assert.That(SubjectLine.For(report), Is.EqualTo("Daily report 2026-09-11 · all green"));
    }

    [Test]
    public void One_warning_is_singular()
    {
        var report = Report([CheckResult.Warn("crosswords", "Puzzle queue", "2 left", TimeSpan.Zero)], []);

        Assert.That(SubjectLine.For(report), Is.EqualTo("🟠 1 warning · Daily report 2026-09-11"));
    }

    [Test]
    public void Two_warnings_is_plural()
    {
        var report = Report(
        [
            CheckResult.Warn("crosswords", "Puzzle queue", "2 left", TimeSpan.Zero),
            CheckResult.Warn("makemered", "Puzzle queue", "1 left", TimeSpan.Zero),
        ], []);

        Assert.That(SubjectLine.For(report), Is.EqualTo("🟠 2 warnings · Daily report 2026-09-11"));
    }

    [Test]
    public void One_failed_check_never_pluralises_failed()
    {
        var report = Report([CheckResult.Fail("crosswords", "Puzzle published", "stale", TimeSpan.Zero)], []);

        Assert.That(SubjectLine.For(report), Is.EqualTo("🔴 1 failed · Daily report 2026-09-11"));
    }

    [Test]
    public void Three_failed_counts_checks_and_source_failures_together()
    {
        var report = Report(
        [
            CheckResult.Fail("crosswords", "Puzzle published", "stale", TimeSpan.Zero),
            CheckResult.Error("makemered", "Puzzle published", "timeout", TimeSpan.Zero),
        ],
        [
            new SourceFailure("makemered", "Metrics", "GoatCounter: 401 Unauthorized"),
        ]);

        Assert.That(SubjectLine.For(report), Is.EqualTo("🔴 3 failed · Daily report 2026-09-11"));
    }

    [Test]
    public void Red_wins_over_amber_when_both_are_present()
    {
        var report = Report(
        [
            CheckResult.Fail("crosswords", "Puzzle published", "stale", TimeSpan.Zero),
            CheckResult.Warn("crosswords", "Puzzle queue", "2 left", TimeSpan.Zero),
        ], []);

        Assert.That(SubjectLine.For(report), Is.EqualTo("🔴 1 failed · Daily report 2026-09-11"));
    }
}
