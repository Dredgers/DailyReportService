using DailyReport.Core.Model;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.Probes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Tests.Probes;

public sealed class CrosswordsPuzzlePublishedProbeTests
{
    // Report goes out the morning of 2026-09-11; today's Copenhagen game day is 2026-09-10T22:00Z..2026-09-11T22:00Z.
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), TestGames.Copenhagen);

    private static CrosswordsPuzzlePublishedProbe Probe(ServiceProvider provider) =>
        provider.GetServices<Core.Abstractions.IHealthProbe>().OfType<CrosswordsPuzzlePublishedProbe>().Single();

    [Test]
    public async Task Passes_when_the_daily_rotated_inside_todays_game_day()
    {
        await using var stub = new StubSite();
        stub.Map("/api/puzzles", ctx => ctx.Response.WriteAsync(
            """{"puzzles":[],"currentId":"3f9a1c2b","currentPublishedAt":"2026-09-10T22:12:00Z"}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
            Assert.That(result.Detail, Is.EqualTo("published 2026-09-11 00:12 Europe/Copenhagen · id 3f9a1c2b"));
        });
    }

    [Test]
    public async Task Fails_when_the_timestamp_is_yesterdays()
    {
        await using var stub = new StubSite();
        stub.Map("/api/puzzles", ctx => ctx.Response.WriteAsync(
            """{"puzzles":[],"currentId":"aaaaaaaa","currentPublishedAt":"2026-09-09T22:12:00Z"}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
            Assert.That(result.Detail, Does.Contain("2026-09-10 00:12"));
        });
    }

    [Test]
    public async Task Fails_when_currentPublishedAt_is_missing()
    {
        await using var stub = new StubSite();
        stub.Map("/api/puzzles", ctx => ctx.Response.WriteAsync("""{"puzzles":[],"currentId":"aaaaaaaa"}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
    }

    [Test]
    public async Task Fails_when_currentPublishedAt_is_unparseable()
    {
        await using var stub = new StubSite();
        stub.Map("/api/puzzles", ctx => ctx.Response.WriteAsync(
            """{"puzzles":[],"currentId":"aaaaaaaa","currentPublishedAt":"not-a-timestamp"}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri), Window, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
    }

    [Test]
    public void Applies_only_to_crosswords()
    {
        using var provider = ProbeHost.Build();
        var probe = Probe(provider);

        Assert.Multiple(() =>
        {
            Assert.That(probe.AppliesTo(TestGames.Crosswords("http://x.invalid")), Is.True);
            Assert.That(probe.AppliesTo(TestGames.MakeMeRed("http://x.invalid")), Is.False);
        });
    }
}
