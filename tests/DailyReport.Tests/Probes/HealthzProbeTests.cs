using System.Diagnostics;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.Probes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Tests.Probes;

public sealed class HealthzProbeTests
{
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), TestGames.Copenhagen);

    private static HealthzProbe Probe(ServiceProvider provider) => provider.GetServices<Core.Abstractions.IHealthProbe>().OfType<HealthzProbe>().Single();

    [Test]
    public async Task Passes_with_status_ok_and_reports_uptime_rooms_and_puzzles()
    {
        await using var stub = new StubSite();
        stub.Map("/healthz", ctx => ctx.Response.WriteAsync("""{"status":"ok","uptime_s":273600,"rooms":2,"puzzles":41}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
            Assert.That(result.Detail, Is.EqualTo("200 ok · up 3d 4h · rooms 2 · puzzles 41"));
        });
    }

    [Test]
    public async Task Passes_with_status_ok_and_no_rooms_or_puzzles()
    {
        await using var stub = new StubSite();
        stub.Map("/healthz", ctx => ctx.Response.WriteAsync("""{"status":"ok","uptime_s":3600}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.MakeMeRed(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
            Assert.That(result.Detail, Is.EqualTo("200 ok · up 1h 0m"));
            Assert.That(result.Detail, Does.Not.Contain("rooms"));
            Assert.That(result.Detail, Does.Not.Contain("puzzles"));
        });
    }

    [Test]
    public async Task Fails_on_503()
    {
        await using var stub = new StubSite();
        stub.Map("/healthz", ctx =>
        {
            ctx.Response.StatusCode = 503;
            return ctx.Response.WriteAsync("""{"status":"error"}""");
        });
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
            Assert.That(result.Detail, Does.Contain("503"));
        });
    }

    [Test]
    public async Task Fails_when_status_is_degraded()
    {
        await using var stub = new StubSite();
        stub.Map("/healthz", ctx => ctx.Response.WriteAsync("""{"status":"degraded","uptime_s":100}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
            Assert.That(result.Detail, Does.Contain("degraded"));
        });
    }

    [Test]
    public async Task Fails_on_non_json_body()
    {
        await using var stub = new StubSite();
        stub.Map("/healthz", ctx => ctx.Response.WriteAsync("not json at all"));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
    }

    [Test]
    public async Task Errors_within_a_couple_of_seconds_when_the_site_hangs()
    {
        await using var stub = new StubSite();
        stub.Map("/healthz", async ctx =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ctx.RequestAborted);
            await ctx.Response.WriteAsync("""{"status":"ok","uptime_s":1}""");
        });
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var sw = Stopwatch.StartNew();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);
        sw.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Error));
            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2.5)));
        });
    }

    [Test]
    public async Task Errors_on_connection_refused()
    {
        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(StubSite.GetClosedPortUri()), Window, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CheckStatus.Error));
    }

    [Test]
    public void Applies_to_every_game()
    {
        using var provider = ProbeHost.Build();
        var probe = Probe(provider);

        Assert.Multiple(() =>
        {
            Assert.That(probe.AppliesTo(TestGames.Crosswords("http://x.invalid")), Is.True);
            Assert.That(probe.AppliesTo(TestGames.MakeMeRed("http://x.invalid")), Is.True);
        });
    }
}
