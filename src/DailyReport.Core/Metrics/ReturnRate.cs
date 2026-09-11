using DailyReport.Core.Time;

namespace DailyReport.Core.Metrics;

/// <summary>A player was active (genuinely completed something) on a game day.</summary>
public readonly record struct PlayerDay(Guid PlayerId, DateOnly Day);

/// <summary>
/// The seven-day return rate, computed the same way for every game from a list of player-days.
/// For a day d: cohort = players active on d−7; returned = cohort members active on any of d−6..d.
/// Rate is null when the cohort is empty (never 0%, never 100% of nobody).
/// </summary>
public static class ReturnRate
{
    public sealed record DayResult(int Cohort, int Returned)
    {
        public double? Rate => Cohort == 0 ? null : 100.0 * Returned / Cohort;
    }

    public static IReadOnlyDictionary<DateOnly, DayResult> Compute(IReadOnlyList<DateOnly> days, IEnumerable<PlayerDay> activity)
    {
        var byDay = activity
            .GroupBy(a => a.Day)
            .ToDictionary(g => g.Key, g => g.Select(a => a.PlayerId).ToHashSet());

        var results = new Dictionary<DateOnly, DayResult>(days.Count);

        foreach (var day in days)
        {
            var cohort = byDay.GetValueOrDefault(ReportWindow.CohortDayFor(day)) ?? [];
            if (cohort.Count == 0)
            {
                results[day] = new DayResult(0, 0);
                continue;
            }

            var (first, last) = ReportWindow.ReturnWindowFor(day);
            var returned = 0;
            foreach (var player in cohort)
            {
                for (var d = first; d <= last; d = d.AddDays(1))
                {
                    if (byDay.TryGetValue(d, out var active) && active.Contains(player))
                    {
                        returned++;
                        break;
                    }
                }
            }

            results[day] = new DayResult(cohort.Count, returned);
        }

        return results;
    }
}
