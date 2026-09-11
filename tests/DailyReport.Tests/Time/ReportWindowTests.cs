using DailyReport.Core.Time;

namespace DailyReport.Tests.Time;

public sealed class ReportWindowTests
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    // Report goes out the morning of Friday 2026-09-11.
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), Copenhagen);

    [Test]
    public void The_game_day_is_yesterday_and_the_comparisons_hang_off_it()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Window.GameDay, Is.EqualTo(new DateOnly(2026, 9, 10)));
            Assert.That(Window.DayBefore, Is.EqualTo(new DateOnly(2026, 9, 9)));
            Assert.That(Window.SameWeekdayLastWeek, Is.EqualTo(new DateOnly(2026, 9, 3)));
            Assert.That(Window.SameWeekdayLastWeek.DayOfWeek, Is.EqualTo(Window.GameDay.DayOfWeek));
            Assert.That(Window.BaselineDays, Is.EqualTo(GameDays.Days(new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 9))));
            Assert.That(Window.SeriesDays, Is.EqualTo(GameDays.Days(new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10))));
        });
    }

    [Test]
    public void Return_cohort_is_seven_days_back_with_a_complete_window()
    {
        var day = Window.GameDay;

        Assert.Multiple(() =>
        {
            Assert.That(ReportWindow.CohortDayFor(day), Is.EqualTo(new DateOnly(2026, 9, 3)));
            Assert.That(ReportWindow.ReturnWindowFor(day), Is.EqualTo((new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 10))));
        });
    }

    [Test]
    public void Query_range_reaches_back_to_the_earliest_cohort_and_ends_at_the_end_of_the_game_day()
    {
        var range = Window.QueryRange;

        Assert.Multiple(() =>
        {
            // Earliest series day is 09-03; its cohort day is 08-27; local midnight CEST = 22:00Z the evening before.
            Assert.That(range.Start, Is.EqualTo(new DateTimeOffset(2026, 8, 26, 22, 0, 0, TimeSpan.Zero)));
            Assert.That(range.End, Is.EqualTo(new DateTimeOffset(2026, 9, 10, 22, 0, 0, TimeSpan.Zero)));
        });
    }

    [Test]
    public void Weekly_slot_defaults_to_monday()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Window.IsWeeklySlot(), Is.False);
            Assert.That(new ReportWindow(new DateOnly(2026, 9, 14), Copenhagen).IsWeeklySlot(), Is.True);
        });
    }
}
