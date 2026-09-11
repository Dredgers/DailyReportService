using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;

namespace DailyReport.Core.Abstractions;

/// <summary>
/// One health check. Decides for itself whether it applies to a game, and never throws: an exception is
/// a <see cref="CheckStatus.Error"/> result, because "we could not check" is itself the finding.
/// </summary>
public interface IHealthProbe
{
    string Name { get; }

    bool AppliesTo(GameOptions game);

    Task<CheckResult> RunAsync(GameOptions game, ReportWindow window, CancellationToken cancellationToken);
}
