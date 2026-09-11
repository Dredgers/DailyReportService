using DailyReport.Core.Configuration;

namespace DailyReport.Infrastructure.Sources.GoatCounter;

/// <summary>One day's site-wide totals as GoatCounter buckets them (in the site's configured time zone).</summary>
public sealed record GoatCounterDay(DateOnly Day, int Visits, int Pageviews);

/// <summary>One day's count for one named event (GoatCounter events are paths flagged as events, e.g. "solve", "solve-four", "share").</summary>
public sealed record GoatCounterEventDay(string EventName, DateOnly Day, int Count);

/// <summary>A referrer and its visit count over a range. <see cref="Name"/> is GoatCounter's display name ("Hacker News", "news.ycombinator.com", "(direct)").</summary>
public sealed record GoatCounterReferrer(string Name, int Count);

/// <summary>
/// Read-only access to GoatCounter's stats API for one site. Implementations resolve the bearer token from
/// <see cref="GoatCounterOptions.TokenEnv"/> via <see cref="Secrets.ISecretReader"/>; a missing token throws
/// <see cref="Secrets.MissingSecretException"/> so the section fails loudly instead of reporting zeros.
/// Ranges are inclusive calendar dates. Days with no traffic are still returned with zeros.
/// </summary>
public interface IGoatCounterClient
{
    Task<IReadOnlyList<GoatCounterDay>> GetDailyTotalsAsync(GoatCounterOptions site, DateOnly first, DateOnly last, CancellationToken cancellationToken);

    /// <summary>Per-event daily counts for every event path seen in the range.</summary>
    Task<IReadOnlyList<GoatCounterEventDay>> GetDailyEventsAsync(GoatCounterOptions site, DateOnly first, DateOnly last, CancellationToken cancellationToken);

    Task<IReadOnlyList<GoatCounterReferrer>> GetTopReferrersAsync(GoatCounterOptions site, DateOnly first, DateOnly last, int limit, CancellationToken cancellationToken);
}
