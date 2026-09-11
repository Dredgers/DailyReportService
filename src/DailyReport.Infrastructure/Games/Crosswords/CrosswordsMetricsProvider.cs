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

namespace DailyReport.Infrastructure.Games.Crosswords;

/// <summary>
/// Competitive Crosswords, Phase 1: arrivals, completions in both lanes, and the signed-in seven-day return rate.
/// Days are bucketed in the game's zone inside Postgres (<c>at time zone</c>), so a 23- or 25-hour day is counted as one day.
///
/// Semantics that are easy to get wrong (see the jobs doc):
///   arrivals            = events landing_view + invite_joined; one row per page load, so this is loads, not visitors.
///   completions (all)   = events game_finished, which guests emit too.
///   completions (signed)= game_results with duration_s IS NOT NULL; a NULL duration is a give-up, and ranked=false replays still count.
///   return rate         = signed-in only; there is no persistent id for guests, by design.
/// </summary>
public sealed class CrosswordsMetricsProvider(
    IGamesDatabase database,
    IGoatCounterClient goatCounter,
    ILogger<CrosswordsMetricsProvider> logger) : IGameMetricsProvider
{
    private static readonly string[] ArrivalEventNames = ["landing_view", "invite_joined"];

    public MetricsProvider Provider => MetricsProvider.Crosswords;

    public async Task<MetricsResult> CollectAsync(GameOptions game, ReportWindow window, CancellationToken cancellationToken)
    {
        var series = new List<DailySeries>();
        var failures = new List<SourceFailure>();

        if (game.Metrics.Arrivals == ArrivalsSource.GoatCounter)
        {
            try
            {
                series.Add(await GoatCounterArrivals.CollectAsync(goatCounter, game, window, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "{Game}: GoatCounter arrivals failed", game.Key);
                failures.Add(new SourceFailure(game.Key, "Arrivals (GoatCounter)", $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        await using var ctx = database.CreateContext();
        var days = window.SeriesDays;
        var range = window.QueryRange;
        var zone = window.Zone.Id;

        if (game.Metrics.Arrivals == ArrivalsSource.EventsTable)
        {
            var arrivals = await CountEventsByDayAsync(ctx, zone, ArrivalEventNames, range, cancellationToken);
            series.Add(Series.Counts(
                new MetricDefinition($"{game.Key}.arrivals", "Traffic", "Arrivals", Lane.All, MetricUnit.Count, "page loads on the landing page or an invite link; not unique visitors"),
                days, arrivals));
        }

        var finished = await CountEventsByDayAsync(ctx, zone, ["game_finished"], range, cancellationToken);
        series.Add(Series.Counts(
            new MetricDefinition($"{game.Key}.completions", "Completions", "Completions", Lane.All, MetricUnit.Count, "game_finished events; guests included"),
            days, finished));

        var genuine = await ctx.Database.SqlQuery<DayCountRow>($"""
            select (r.completed_at at time zone {zone})::date as "Day", count(*)::int as "N"
            from public.game_results r
            where r.duration_s is not null
              and r.completed_at >= {range.Start} and r.completed_at < {range.End}
            group by 1
            """).ToListAsync(cancellationToken);
        series.Add(Series.Counts(
            new MetricDefinition($"{game.Key}.completions.signed_in", "Completions", "Completions", Lane.SignedIn, MetricUnit.Count, "genuine solves by signed-in players; give-ups excluded, replays included"),
            days, genuine.ToDictionary(r => r.Day, r => r.N)));

        var activity = await ctx.Database.SqlQuery<PlayerDayRow>($"""
            select distinct r.user_id as "PlayerId", (r.completed_at at time zone {zone})::date as "Day"
            from public.game_results r
            where r.duration_s is not null
              and r.completed_at >= {range.Start} and r.completed_at < {range.End}
            """).ToListAsync(cancellationToken);
        var returns = ReturnRate.Compute(days, activity.Select(a => new PlayerDay(a.PlayerId, a.Day)));

        series.Add(Series.Values(
            new MetricDefinition($"{game.Key}.return_rate", "Return", "7-day return rate", Lane.SignedIn, MetricUnit.Percent, "of signed-in players who completed a puzzle seven days earlier, the share who completed another within the week"),
            days, d => returns[d].Rate));
        series.Add(Series.Values(
            new MetricDefinition($"{game.Key}.return_cohort", "Return", "Return cohort", Lane.SignedIn, MetricUnit.Count, "signed-in players who completed a puzzle seven days earlier"),
            days, d => returns[d].Cohort));

        return new MetricsResult(series, failures);
    }

    private static async Task<Dictionary<DateOnly, int>> CountEventsByDayAsync(
        GamesDbContext ctx, string zone, string[] names, UtcRange range, CancellationToken cancellationToken)
    {
        var rows = await ctx.Database.SqlQuery<DayCountRow>($"""
            select (e.created_at at time zone {zone})::date as "Day", count(*)::int as "N"
            from public.events e
            where e.name = any({names})
              and e.created_at >= {range.Start} and e.created_at < {range.End}
            group by 1
            """).ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Day, r => r.N);
    }
}
