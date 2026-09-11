using DailyReport.Core.Model;

namespace DailyReport.Core.Metrics;

/// <summary>Builders that turn query results into <see cref="DailySeries"/> with the right null-vs-zero semantics.</summary>
public static class Series
{
    /// <summary>
    /// A count per day where the query covered every day: a day with no rows is 0, not "no data".
    /// </summary>
    public static DailySeries Counts(MetricDefinition definition, IReadOnlyList<DateOnly> days, IReadOnlyDictionary<DateOnly, int> counts)
    {
        var byDay = new Dictionary<DateOnly, double?>(days.Count);
        foreach (var day in days)
        {
            byDay[day] = counts.TryGetValue(day, out var n) ? n : 0;
        }

        return new DailySeries(definition, byDay);
    }

    /// <summary>A value per day that may legitimately be absent (a rate with no cohort).</summary>
    public static DailySeries Values(MetricDefinition definition, IReadOnlyList<DateOnly> days, Func<DateOnly, double?> value)
    {
        var byDay = new Dictionary<DateOnly, double?>(days.Count);
        foreach (var day in days)
        {
            byDay[day] = value(day);
        }

        return new DailySeries(definition, byDay);
    }
}
