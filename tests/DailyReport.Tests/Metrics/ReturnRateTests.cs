using DailyReport.Core.Metrics;

namespace DailyReport.Tests.Metrics;

public sealed class ReturnRateTests
{
    private static readonly DateOnly Day = new(2026, 9, 10);

    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Test]
    public void Cohort_is_the_players_active_seven_days_earlier_and_returned_means_any_day_since()
    {
        var activity = new[]
        {
            new PlayerDay(A, Day.AddDays(-7)), // cohort, returns on the last possible day
            new PlayerDay(A, Day),
            new PlayerDay(B, Day.AddDays(-7)), // cohort, returns on the first possible day
            new PlayerDay(B, Day.AddDays(-6)),
            new PlayerDay(C, Day.AddDays(-7)), // cohort, never returns (activity the day before does not count)
            new PlayerDay(C, Day.AddDays(-8)),
        };

        var result = ReturnRate.Compute([Day], activity)[Day];

        Assert.Multiple(() =>
        {
            Assert.That(result.Cohort, Is.EqualTo(3));
            Assert.That(result.Returned, Is.EqualTo(2));
            Assert.That(result.Rate, Is.EqualTo(200.0 / 3).Within(1e-9));
        });
    }

    [Test]
    public void Empty_cohort_has_no_rate()
    {
        var result = ReturnRate.Compute([Day], [new PlayerDay(A, Day)])[Day];

        Assert.Multiple(() =>
        {
            Assert.That(result.Cohort, Is.EqualTo(0));
            Assert.That(result.Rate, Is.Null);
        });
    }

    [Test]
    public void Playing_twice_in_the_window_counts_once()
    {
        var activity = new[]
        {
            new PlayerDay(A, Day.AddDays(-7)),
            new PlayerDay(A, Day.AddDays(-5)),
            new PlayerDay(A, Day.AddDays(-4)),
            new PlayerDay(A, Day),
        };

        var result = ReturnRate.Compute([Day], activity)[Day];

        Assert.That((result.Cohort, result.Returned), Is.EqualTo((1, 1)));
    }

    [Test]
    public void Every_requested_day_gets_a_result()
    {
        var days = new[] { Day.AddDays(-1), Day };
        var results = ReturnRate.Compute(days, []);

        Assert.That(results.Keys, Is.EquivalentTo(days));
    }
}
