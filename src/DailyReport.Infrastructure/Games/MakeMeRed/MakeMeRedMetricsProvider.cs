using System.Globalization;
using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using DailyReport.Core.Metrics;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.Data;
using DailyReport.Infrastructure.Games.Shared;
using DailyReport.Infrastructure.Sources.GoatCounter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DailyReport.Infrastructure.Games.MakeMeRed;

/// <summary>
/// Make Me Red, Phase 1. The game day is the UTC date, and <c>mmr_results.date</c> already is that date as text,
/// so the database side needs no zone arithmetic. GoatCounter supplies everything guests do (visits, solve events
/// per mode, shares); the database supplies the signed-in lane and the return rate.
///
///   completions (all)    = GoatCounter events solve / solve-four / solve-five, one per mode.
///   completions (signed) = mmr_results rows with completed_at set, per mode.
///   return rate          = signed-in only, any mode.
/// </summary>
public sealed class MakeMeRedMetricsProvider(
    IGamesDatabase database,
    IGoatCounterClient goatCounter,
    ILogger<MakeMeRedMetricsProvider> logger) : IGameMetricsProvider
{
    /// <summary>Mode → GoatCounter event name. "solve" without a suffix is the three-colour game.</summary>
    public static readonly IReadOnlyList<(string Mode, string Event)> Modes =
    [
        ("three", "solve"),
        ("four", "solve-four"),
        ("five", "solve-five"),
    ];

    public MetricsProvider Provider => MetricsProvider.MakeMeRed;

    public async Task<MetricsResult> CollectAsync(GameOptions game, ReportWindow window, CancellationToken cancellationToken)
    {
        var series = new List<DailySeries>();
        var failures = new List<SourceFailure>();
        var days = window.SeriesDays;

        if (game.GoatCounter is { } site)
        {
            try
            {
                if (game.Metrics.Arrivals == ArrivalsSource.GoatCounter)
                {
                    series.Add(await GoatCounterArrivals.CollectAsync(goatCounter, game, window, cancellationToken));
                }

                var events = await goatCounter.GetDailyEventsAsync(site, days[0], days[^1], cancellationToken);
                var byEvent = events.GroupBy(e => e.EventName).ToDictionary(g => g.Key, g => g.ToDictionary(e => e.Day, e => e.Count));

                foreach (var (mode, eventName) in Modes)
                {
                    series.Add(Series.Counts(
                        new MetricDefinition($"{game.Key}.completions.{mode}", "Completions", $"Completions · {mode}", Lane.All, MetricUnit.Count, $"GoatCounter '{eventName}' events; guests included"),
                        days, byEvent.GetValueOrDefault(eventName) ?? new Dictionary<DateOnly, int>()));
                }

                series.Add(Series.Counts(
                    new MetricDefinition($"{game.Key}.shares", "Engagement", "Shares", Lane.All, MetricUnit.Count, "GoatCounter 'share' events"),
                    days, byEvent.GetValueOrDefault("share") ?? new Dictionary<DateOnly, int>()));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "{Game}: GoatCounter failed", game.Key);
                failures.Add(new SourceFailure(game.Key, "GoatCounter", $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        await using var ctx = database.CreateContext();
        var firstDate = DateKey(ReportWindow.CohortDayFor(days[0]));
        var lastDate = DateKey(days[^1]);

        var completed = await ctx.MmrResults
            .Where(r => r.CompletedAt != null && string.Compare(r.Date, firstDate) >= 0 && string.Compare(r.Date, lastDate) <= 0)
            .Select(r => new { r.UserId, r.Date, r.Mode })
            .ToListAsync(cancellationToken);

        foreach (var (mode, _) in Modes)
        {
            var counts = completed
                .Where(r => r.Mode == mode)
                .GroupBy(r => ParseDate(r.Date))
                .ToDictionary(g => g.Key, g => g.Count());

            series.Add(Series.Counts(
                new MetricDefinition($"{game.Key}.completions.{mode}.signed_in", "Completions", $"Completions · {mode}", Lane.SignedIn, MetricUnit.Count, "signed-in players with a recorded completion"),
                days, counts));
        }

        var returns = ReturnRate.Compute(days, completed.Select(r => new PlayerDay(r.UserId, ParseDate(r.Date))).Distinct());

        series.Add(Series.Values(
            new MetricDefinition($"{game.Key}.return_rate", "Return", "7-day return rate", Lane.SignedIn, MetricUnit.Percent, "of signed-in players who completed a Red seven days earlier, the share who completed another within the week"),
            days, d => returns[d].Rate));
        series.Add(Series.Values(
            new MetricDefinition($"{game.Key}.return_cohort", "Return", "Return cohort", Lane.SignedIn, MetricUnit.Count, "signed-in players who completed a Red seven days earlier"),
            days, d => returns[d].Cohort));

        return new MetricsResult(series, failures);
    }

    private static string DateKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string key) => DateOnly.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
