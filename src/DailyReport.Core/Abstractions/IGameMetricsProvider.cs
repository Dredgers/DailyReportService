using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;

namespace DailyReport.Core.Abstractions;

/// <summary>
/// The game-specific queries. One implementation per <see cref="MetricsProvider"/>; the game instance comes from config.
/// Must return a value (or null) for every day in <see cref="ReportWindow.SeriesDays"/> for every series it emits.
/// May throw: the runner turns that into a <see cref="SourceFailure"/> and the email still goes out.
/// </summary>
public interface IGameMetricsProvider
{
    MetricsProvider Provider { get; }

    Task<IReadOnlyList<DailySeries>> CollectAsync(GameOptions game, ReportWindow window, CancellationToken cancellationToken);
}
