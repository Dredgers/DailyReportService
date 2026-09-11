using DailyReport.Core.Configuration;
using DailyReport.Core.Metrics;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.Sources.GoatCounter;

namespace DailyReport.Infrastructure.Games.Shared;

/// <summary>
/// The arrivals line every game with a GoatCounter site shares. GoatCounter's v0 API exposes one number per day,
/// visitors (a cookieless daily-unique count), so that is what "arrivals" means for a GoatCounter game.
/// </summary>
public static class GoatCounterArrivals
{
    public static async Task<DailySeries> CollectAsync(
        IGoatCounterClient client, GameOptions game, ReportWindow window, CancellationToken cancellationToken)
    {
        var site = game.GoatCounter ?? throw new InvalidOperationException($"{game.Key}: Arrivals is GoatCounter but no site is configured.");
        var days = window.SeriesDays;
        var totals = await client.GetDailyTotalsAsync(site, days[0], days[^1], cancellationToken);

        return Series.Counts(
            new MetricDefinition($"{game.Key}.arrivals", "Traffic", "Arrivals", Lane.All, MetricUnit.Count, "GoatCounter visitors (cookieless daily uniques)"),
            days,
            totals.ToDictionary(t => t.Day, t => t.Visits));
    }
}
