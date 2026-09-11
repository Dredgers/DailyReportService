using DailyReport.Core.Time;

namespace DailyReport.Tests.Time;

public sealed class GameDaysTests
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Test]
    public void Ordinary_copenhagen_day_is_24_hours_starting_at_22Z_in_summer()
    {
        var range = GameDays.RangeOf(new DateOnly(2026, 9, 10), Copenhagen);

        Assert.Multiple(() =>
        {
            Assert.That(range.Start, Is.EqualTo(new DateTimeOffset(2026, 9, 9, 22, 0, 0, TimeSpan.Zero)));
            Assert.That(range.End, Is.EqualTo(new DateTimeOffset(2026, 9, 10, 22, 0, 0, TimeSpan.Zero)));
            Assert.That(range.Length, Is.EqualTo(TimeSpan.FromHours(24)));
        });
    }

    [Test]
    public void Spring_forward_day_is_23_hours()
    {
        // 2026-03-29 02:00 CET → 03:00 CEST.
        var range = GameDays.RangeOf(new DateOnly(2026, 3, 29), Copenhagen);

        Assert.Multiple(() =>
        {
            Assert.That(range.Start, Is.EqualTo(new DateTimeOffset(2026, 3, 28, 23, 0, 0, TimeSpan.Zero)));
            Assert.That(range.End, Is.EqualTo(new DateTimeOffset(2026, 3, 29, 22, 0, 0, TimeSpan.Zero)));
            Assert.That(range.Length, Is.EqualTo(TimeSpan.FromHours(23)));
        });
    }

    [Test]
    public void Fall_back_day_is_25_hours()
    {
        // 2026-10-25 03:00 CEST → 02:00 CET.
        var range = GameDays.RangeOf(new DateOnly(2026, 10, 25), Copenhagen);

        Assert.Multiple(() =>
        {
            Assert.That(range.Start, Is.EqualTo(new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero)));
            Assert.That(range.End, Is.EqualTo(new DateTimeOffset(2026, 10, 25, 23, 0, 0, TimeSpan.Zero)));
            Assert.That(range.Length, Is.EqualTo(TimeSpan.FromHours(25)));
        });
    }

    [Test]
    public void Consecutive_days_tile_without_gap_or_overlap_across_dst()
    {
        var before = GameDays.RangeOf(new DateOnly(2026, 10, 24), Copenhagen);
        var during = GameDays.RangeOf(new DateOnly(2026, 10, 25), Copenhagen);
        var after = GameDays.RangeOf(new DateOnly(2026, 10, 26), Copenhagen);

        Assert.Multiple(() =>
        {
            Assert.That(before.End, Is.EqualTo(during.Start));
            Assert.That(during.End, Is.EqualTo(after.Start));
            Assert.That(during.Contains(during.End), Is.False, "half-open");
            Assert.That(during.Contains(during.Start), Is.True);
        });
    }

    [Test]
    public void Utc_day_is_plain()
    {
        var range = GameDays.RangeOf(new DateOnly(2026, 9, 10), Utc);

        Assert.Multiple(() =>
        {
            Assert.That(range.Start, Is.EqualTo(new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(range.Length, Is.EqualTo(TimeSpan.FromHours(24)));
        });
    }

    [Test]
    public void DateIn_rolls_the_date_at_local_midnight_not_utc_midnight()
    {
        // 23:30Z on the 10th is 01:30 on the 11th in Copenhagen (CEST).
        var instant = new DateTimeOffset(2026, 9, 10, 23, 30, 0, TimeSpan.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(GameDays.DateIn(instant, Copenhagen), Is.EqualTo(new DateOnly(2026, 9, 11)));
            Assert.That(GameDays.DateIn(instant, Utc), Is.EqualTo(new DateOnly(2026, 9, 10)));
        });
    }

    [Test]
    public void Multi_day_range_and_day_list_agree()
    {
        var first = new DateOnly(2026, 9, 1);
        var last = new DateOnly(2026, 9, 7);

        Assert.Multiple(() =>
        {
            Assert.That(GameDays.Days(first, last), Has.Count.EqualTo(7));
            Assert.That(GameDays.RangeOf(first, last, Utc).Length, Is.EqualTo(TimeSpan.FromDays(7)));
            Assert.That(() => GameDays.Days(last, first), Throws.ArgumentException);
        });
    }
}
