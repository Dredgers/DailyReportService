namespace DailyReport.Core.Time;

/// <summary>
/// Every date a report needs for one game, derived from the report date D (the morning the email goes out)
/// and the game's own zone. The day being reported is always D−1 in that zone; the zone only decides where
/// that day starts and ends in UTC. Baselines and cohorts are all relative to that day.
/// </summary>
public sealed record ReportWindow(DateOnly ReportDate, TimeZoneInfo Zone)
{
    /// <summary>D−1: the day the report is about.</summary>
    public DateOnly GameDay => ReportDate.AddDays(-1);

    /// <summary>D−2.</summary>
    public DateOnly DayBefore => GameDay.AddDays(-1);

    /// <summary>D−8: the same weekday one week earlier.</summary>
    public DateOnly SameWeekdayLastWeek => GameDay.AddDays(-7);

    /// <summary>D−8..D−2: the seven days before the game day, for the seven-day mean.</summary>
    public IReadOnlyList<DateOnly> BaselineDays => GameDays.Days(GameDay.AddDays(-7), GameDay.AddDays(-1));

    /// <summary>
    /// D−8..D−1: the days a provider must produce a value for so that every metric has a value, a day-before,
    /// a seven-day mean and a same-weekday comparison.
    /// </summary>
    public IReadOnlyList<DateOnly> SeriesDays => GameDays.Days(GameDay.AddDays(-7), GameDay);

    /// <summary>The cohort for the return rate shown on <paramref name="day"/>: players who completed seven days earlier.</summary>
    public static DateOnly CohortDayFor(DateOnly day) => day.AddDays(-7);

    /// <summary>The return window for the cohort of <paramref name="day"/>: the six days after the cohort day up to and including the day.</summary>
    public static (DateOnly First, DateOnly Last) ReturnWindowFor(DateOnly day) => (day.AddDays(-6), day);

    /// <summary>
    /// The widest UTC range any Phase 1 query needs: the cohort day of the earliest series day through the end of the game day.
    /// D−15 00:00 local to D 00:00 local.
    /// </summary>
    public UtcRange QueryRange => GameDays.RangeOf(CohortDayFor(SeriesDays[0]), GameDay, Zone);

    public UtcRange RangeOf(DateOnly day) => GameDays.RangeOf(day, Zone);

    public UtcRange RangeOf(DateOnly first, DateOnly lastInclusive) => GameDays.RangeOf(first, lastInclusive, Zone);

    /// <summary>Whether the report date's weekday matches; used for weekly-cadence probes.</summary>
    public bool IsWeeklySlot(DayOfWeek day = DayOfWeek.Monday) => ReportDate.DayOfWeek == day;
}
