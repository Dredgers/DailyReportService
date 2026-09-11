using DailyReport.Core.Time;

namespace DailyReport.Tests.Time;

public sealed class ReportScheduleTests
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private static readonly TimeOnly Seven = new(7, 0);

    private static DateTimeOffset Z(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    [Test]
    public void Next_run_is_today_before_the_slot_and_tomorrow_at_or_after_it()
    {
        Assert.Multiple(() =>
        {
            // 06:00 CEST → today 07:00 CEST = 05:00Z.
            Assert.That(ReportSchedule.NextRunUtc(Z(9, 11, 4), Seven, Copenhagen), Is.EqualTo(Z(9, 11, 5)));
            // Exactly at the slot → strictly after, so tomorrow.
            Assert.That(ReportSchedule.NextRunUtc(Z(9, 11, 5), Seven, Copenhagen), Is.EqualTo(Z(9, 12, 5)));
            // 08:00 CEST → tomorrow.
            Assert.That(ReportSchedule.NextRunUtc(Z(9, 11, 6), Seven, Copenhagen), Is.EqualTo(Z(9, 12, 5)));
        });
    }

    [Test]
    public void Next_run_crosses_the_autumn_dst_change_at_the_same_wall_clock_time()
    {
        // Saturday 24 Oct 07:30 CEST (05:30Z). Sunday 25 Oct is the fall-back day; 07:00 CET is 06:00Z.
        Assert.That(ReportSchedule.NextRunUtc(Z(10, 24, 5, 30), Seven, Copenhagen), Is.EqualTo(Z(10, 25, 6)));
    }

    [Test]
    public void Next_run_crosses_the_spring_dst_change_at_the_same_wall_clock_time()
    {
        // Saturday 28 Mar 07:30 CET (06:30Z). Sunday 29 Mar is the spring-forward day; 07:00 CEST is 05:00Z.
        Assert.That(ReportSchedule.NextRunUtc(Z(3, 28, 6, 30), Seven, Copenhagen), Is.EqualTo(Z(3, 29, 5)));
    }

    [Test]
    public void Report_date_is_the_local_date_of_the_run()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReportSchedule.ReportDateAt(Z(9, 11, 5), Copenhagen), Is.EqualTo(new DateOnly(2026, 9, 11)));
            // 22:30Z on the 10th is already the 11th in Copenhagen.
            Assert.That(ReportSchedule.ReportDateAt(Z(9, 10, 22, 30), Copenhagen), Is.EqualTo(new DateOnly(2026, 9, 11)));
        });
    }

    [Test]
    public void Catch_up_is_due_only_after_the_slot_and_only_if_today_was_not_sent()
    {
        var today = new DateOnly(2026, 9, 11);

        Assert.Multiple(() =>
        {
            Assert.That(ReportSchedule.IsCatchUpDue(Z(9, 11, 6), Seven, Copenhagen, null), Is.True, "08:00 local, never sent");
            Assert.That(ReportSchedule.IsCatchUpDue(Z(9, 11, 6), Seven, Copenhagen, today.AddDays(-1)), Is.True, "08:00 local, yesterday sent");
            Assert.That(ReportSchedule.IsCatchUpDue(Z(9, 11, 6), Seven, Copenhagen, today), Is.False, "already sent today");
            Assert.That(ReportSchedule.IsCatchUpDue(Z(9, 11, 4), Seven, Copenhagen, null), Is.False, "06:00 local, slot not reached");
            Assert.That(ReportSchedule.IsCatchUpDue(Z(9, 11, 5), Seven, Copenhagen, null), Is.True, "exactly at the slot counts");
        });
    }
}
