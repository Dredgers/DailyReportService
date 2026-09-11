namespace DailyReport.Core.Time;

/// <summary>
/// When the next report is due and whether one was missed. Pure, so the scheduler service is a thin loop and
/// all the calendar reasoning is unit-tested, DST included.
/// </summary>
public static class ReportSchedule
{
    /// <summary>The UTC instant of the next send at <paramref name="sendAt"/> local time in <paramref name="zone"/>, strictly after <paramref name="nowUtc"/>.</summary>
    public static DateTimeOffset NextRunUtc(DateTimeOffset nowUtc, TimeOnly sendAt, TimeZoneInfo zone)
    {
        var today = GameDays.DateIn(nowUtc, zone);
        var candidate = LocalToUtc(today, sendAt, zone);
        return candidate > nowUtc ? candidate : LocalToUtc(today.AddDays(1), sendAt, zone);
    }

    /// <summary>The report date a run at <paramref name="instantUtc"/> is for: the calendar date in the schedule zone.</summary>
    public static DateOnly ReportDateAt(DateTimeOffset instantUtc, TimeZoneInfo zone) => GameDays.DateIn(instantUtc, zone);

    /// <summary>
    /// On start-up: today's slot has passed and today's report has not been sent, so run now rather than wait a day.
    /// </summary>
    public static bool IsCatchUpDue(DateTimeOffset nowUtc, TimeOnly sendAt, TimeZoneInfo zone, DateOnly? lastSentReportDate)
    {
        var today = GameDays.DateIn(nowUtc, zone);
        var slot = LocalToUtc(today, sendAt, zone);
        return nowUtc >= slot && lastSentReportDate != today;
    }

    private static DateTimeOffset LocalToUtc(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);

        // If the wall-clock time does not exist on a spring-forward day, run at the first valid minute after it.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
