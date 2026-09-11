namespace DailyReport.Core.Time;

/// <summary>A half-open UTC interval [Start, End). Every database query is bounded by one of these.</summary>
public readonly record struct UtcRange(DateTimeOffset Start, DateTimeOffset End)
{
    public TimeSpan Length => End - Start;

    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;
}

/// <summary>
/// Day arithmetic in a game's zone. A "day" is local midnight to the next local midnight, expressed in UTC,
/// so a 23-hour or 25-hour DST day is exactly that long and no event is counted twice or dropped.
/// </summary>
public static class GameDays
{
    /// <summary>The calendar date in <paramref name="zone"/> at <paramref name="instant"/>.</summary>
    public static DateOnly DateIn(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>Local midnight of <paramref name="day"/> as a UTC instant.</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // A few zones skip midnight on their spring-forward day; neither of ours does, but be correct anyway.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        // For an ambiguous local time GetUtcOffset returns the standard offset; midnight is never ambiguous in our zones.
        var offset = zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static UtcRange RangeOf(DateOnly day, TimeZoneInfo zone) =>
        new(StartOfDayUtc(day, zone), StartOfDayUtc(day.AddDays(1), zone));

    public static UtcRange RangeOf(DateOnly first, DateOnly lastInclusive, TimeZoneInfo zone)
    {
        if (lastInclusive < first)
        {
            throw new ArgumentException("lastInclusive is before first.", nameof(lastInclusive));
        }

        return new UtcRange(StartOfDayUtc(first, zone), StartOfDayUtc(lastInclusive.AddDays(1), zone));
    }

    public static IReadOnlyList<DateOnly> Days(DateOnly first, DateOnly lastInclusive)
    {
        if (lastInclusive < first)
        {
            throw new ArgumentException("lastInclusive is before first.", nameof(lastInclusive));
        }

        var days = new List<DateOnly>(lastInclusive.DayNumber - first.DayNumber + 1);
        for (var d = first; d <= lastInclusive; d = d.AddDays(1))
        {
            days.Add(d);
        }

        return days;
    }
}
