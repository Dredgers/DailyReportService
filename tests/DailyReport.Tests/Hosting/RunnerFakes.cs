using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;

namespace DailyReport.Tests.Hosting;

public sealed class FakeProvider(MetricsProvider provider, Func<GameOptions, ReportWindow, CancellationToken, Task<MetricsResult>> collect) : IGameMetricsProvider
{
    public MetricsProvider Provider => provider;

    public Task<MetricsResult> CollectAsync(GameOptions game, ReportWindow window, CancellationToken cancellationToken) => collect(game, window, cancellationToken);

    public static FakeProvider Healthy(MetricsProvider provider, double value = 10) => new(provider, (game, window, _) =>
    {
        var byDay = window.SeriesDays.ToDictionary(d => d, d => (double?)value);
        var series = new DailySeries(new MetricDefinition($"{game.Key}.arrivals", "Traffic", "Arrivals", Lane.All, MetricUnit.Count), byDay);
        return Task.FromResult(new MetricsResult([series], []));
    });

    public static FakeProvider Throwing(MetricsProvider provider, string message) =>
        new(provider, (_, _, _) => throw new InvalidOperationException(message));

    public static FakeProvider Hanging(MetricsProvider provider) =>
        new(provider, async (_, _, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return MetricsResult.Empty; });
}

public sealed class FakeProbe(string name, Func<GameOptions, bool> applies, Func<GameOptions, ReportWindow, CancellationToken, Task<CheckResult>> run) : IHealthProbe
{
    public string Name => name;

    public bool AppliesTo(GameOptions game) => applies(game);

    public Task<CheckResult> RunAsync(GameOptions game, ReportWindow window, CancellationToken cancellationToken) => run(game, window, cancellationToken);

    public static FakeProbe Passing(string name) => new(name, _ => true, (g, _, _) => Task.FromResult(CheckResult.Pass(g.Key, name, "ok", TimeSpan.FromMilliseconds(5))));

    public static FakeProbe Failing(string name) => new(name, _ => true, (g, _, _) => Task.FromResult(CheckResult.Fail(g.Key, name, "broken", TimeSpan.FromMilliseconds(5))));

    public static FakeProbe Throwing(string name) => new(name, _ => true, (_, _, _) => throw new TimeoutException("probe blew up"));
}

public sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Exception? Throw { get; set; }

    public Task<EmailReceipt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Throw is not null)
        {
            throw Throw;
        }

        Sent.Add(message);
        return Task.FromResult(new EmailReceipt($"msg-{Sent.Count}"));
    }
}

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
