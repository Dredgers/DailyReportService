using DailyReport.Core.Model;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.Probes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Tests.Probes;

public sealed class MakeMeRedPuzzlePublishedProbeTests
{
    // Same epoch fact the probe is built from (Red #1 = 2026-08-14), kept independent so the test is a real check.
    private static readonly DateOnly Epoch = new(2026, 8, 14);
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), TestGames.Copenhagen);
    private static readonly int ExpectedNumber = Window.ReportDate.DayNumber - Epoch.DayNumber + 1;

    private static MakeMeRedPuzzlePublishedProbe Probe(ServiceProvider provider) =>
        provider.GetServices<Core.Abstractions.IHealthProbe>().OfType<MakeMeRedPuzzlePublishedProbe>().Single();

    [Test]
    public async Task Passes_when_date_and_number_both_match()
    {
        await using var stub = new StubSite();
        stub.Map("/api/puzzle/today", ctx => ctx.Response.WriteAsync(
            $$$"""{"date":"2026-09-11","puzzleNumber":{{{ExpectedNumber}}},"scramble":[],"par":4,"colors":{}}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.MakeMeRed(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
            Assert.That(result.Detail, Is.EqualTo($"Red #{ExpectedNumber} for 2026-09-11"));
        });
    }

    [Test]
    public async Task Fails_when_the_date_is_wrong()
    {
        await using var stub = new StubSite();
        stub.Map("/api/puzzle/today", ctx => ctx.Response.WriteAsync(
            $$$"""{"date":"2026-09-10","puzzleNumber":{{{ExpectedNumber}}},"scramble":[],"par":4,"colors":{}}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.MakeMeRed(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
            Assert.That(result.Detail, Does.Contain("date"));
            Assert.That(result.Detail, Does.Not.Contain("puzzleNumber"));
        });
    }

    [Test]
    public async Task Fails_when_the_number_is_wrong()
    {
        await using var stub = new StubSite();
        stub.Map("/api/puzzle/today", ctx => ctx.Response.WriteAsync(
            """{"date":"2026-09-11","puzzleNumber":99999,"scramble":[],"par":4,"colors":{}}"""));
        await stub.StartAsync();

        using var provider = ProbeHost.Build();
        var result = await Probe(provider).RunAsync(TestGames.MakeMeRed(stub.BaseUri), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
            Assert.That(result.Detail, Does.Contain("puzzleNumber"));
            Assert.That(result.Detail, Does.Not.Contain("date "));
        });
    }

    [Test]
    public void Applies_only_to_makemered()
    {
        using var provider = ProbeHost.Build();
        var probe = Probe(provider);

        Assert.Multiple(() =>
        {
            Assert.That(probe.AppliesTo(TestGames.MakeMeRed("http://x.invalid")), Is.True);
            Assert.That(probe.AppliesTo(TestGames.Crosswords("http://x.invalid")), Is.False);
        });
    }
}
