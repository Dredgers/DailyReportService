using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Data;
using DailyReport.Infrastructure.Secrets;
using DailyReport.Infrastructure.Sources.GoatCounter;

namespace DailyReport.IntegrationTests;

/// <summary>Hands out contexts connected as report_ro, exactly as production does.</summary>
public sealed class ReportRoDatabase : IGamesDatabase
{
    public GamesDbContext CreateContext() => TestData.ReportRo();
}

/// <summary>Scripted GoatCounter: fixed answers per day, or a thrown exception to simulate an outage or a missing token.</summary>
public sealed class FakeGoatCounterClient : IGoatCounterClient
{
    public Dictionary<DateOnly, int> Visits { get; } = [];

    public Dictionary<(string Event, DateOnly Day), int> Events { get; } = [];

    public Exception? Throw { get; set; }

    public int Calls { get; private set; }

    public static FakeGoatCounterClient MissingToken(string tokenEnv = "GOATCOUNTER_MMR_TOKEN") => new() { Throw = new MissingSecretException(tokenEnv) };

    public Task<IReadOnlyList<GoatCounterDay>> GetDailyTotalsAsync(GoatCounterOptions site, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        Calls++;
        if (Throw is not null)
        {
            throw Throw;
        }

        var days = new List<GoatCounterDay>();
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            var n = Visits.GetValueOrDefault(d);
            days.Add(new GoatCounterDay(d, n, n));
        }

        return Task.FromResult<IReadOnlyList<GoatCounterDay>>(days);
    }

    public Task<IReadOnlyList<GoatCounterEventDay>> GetDailyEventsAsync(GoatCounterOptions site, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        Calls++;
        if (Throw is not null)
        {
            throw Throw;
        }

        var rows = Events
            .Where(kv => kv.Key.Day >= first && kv.Key.Day <= last)
            .Select(kv => new GoatCounterEventDay(kv.Key.Event, kv.Key.Day, kv.Value))
            .ToList();

        return Task.FromResult<IReadOnlyList<GoatCounterEventDay>>(rows);
    }

    public Task<IReadOnlyList<GoatCounterReferrer>> GetTopReferrersAsync(GoatCounterOptions site, DateOnly first, DateOnly last, int limit, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<GoatCounterReferrer>>([]);
    }
}
