using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;

namespace DailyReport.Core.Abstractions;

/// <summary>
/// What a provider produced: every series it could build, and a failure per source it could not reach.
/// A provider with two sources (a database and an analytics API) reports the survivor's numbers and the casualty's error.
/// </summary>
public sealed record MetricsResult(IReadOnlyList<DailySeries> Series, IReadOnlyList<SourceFailure> Failures)
{
    public static readonly MetricsResult Empty = new([], []);
}

/// <summary>
/// The game-specific queries. One implementation per <see cref="MetricsProvider"/>; the game instance comes from config.
/// Must return a value (or null) for every day in <see cref="ReportWindow.SeriesDays"/> for every series it emits.
/// May still throw for a failure that takes the whole section down; the runner turns that into a single
/// <see cref="SourceFailure"/> and the email still goes out.
/// </summary>
public interface IGameMetricsProvider
{
    MetricsProvider Provider { get; }

    Task<MetricsResult> CollectAsync(GameOptions game, ReportWindow window, CancellationToken cancellationToken);
}
