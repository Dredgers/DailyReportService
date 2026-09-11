using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.Probes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Tests.Probes;

public sealed class PuzzleQueueProbeTests
{
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), TestGames.Copenhagen);

    private static PuzzleQueueProbe Probe(ServiceProvider provider) =>
        provider.GetServices<Core.Abstractions.IHealthProbe>().OfType<PuzzleQueueProbe>().Single();

    private static ServiceProvider BuildWithSource(string url, string key) =>
        ProbeHost.Build(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{PuzzleSourceOptions.SectionName}:Url"] = url,
                [$"{PuzzleSourceOptions.SectionName}:Key"] = key,
            })
            .Build());

    [Test]
    public async Task Passes_when_the_queue_is_at_or_above_the_minimum()
    {
        await using var stub = new StubSite();
        stub.Map("/rest/v1/puzzles", ctx =>
        {
            ctx.Response.Headers["Content-Range"] = "0-0/7";
            return ctx.Response.WriteAsync("[]");
        });
        await stub.StartAsync();

        using var provider = BuildWithSource(stub.BaseUri, "test-key");
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri, puzzleQueueMin: 3), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Pass));
            Assert.That(result.Detail, Is.EqualTo("7 approved puzzles queued"));
        });
    }

    [Test]
    public async Task Warns_when_the_queue_is_below_the_minimum_but_not_empty()
    {
        await using var stub = new StubSite();
        stub.Map("/rest/v1/puzzles", ctx =>
        {
            ctx.Response.Headers["Content-Range"] = "0-0/2";
            return ctx.Response.WriteAsync("[]");
        });
        await stub.StartAsync();

        using var provider = BuildWithSource(stub.BaseUri, "test-key");
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri, puzzleQueueMin: 3), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Warn));
            Assert.That(result.Detail, Is.EqualTo("2 approved puzzles queued (min 3)"));
        });
    }

    [Test]
    public async Task Fails_when_the_queue_is_empty()
    {
        await using var stub = new StubSite();
        stub.Map("/rest/v1/puzzles", ctx =>
        {
            ctx.Response.Headers["Content-Range"] = "*/0";
            return ctx.Response.WriteAsync("[]");
        });
        await stub.StartAsync();

        using var provider = BuildWithSource(stub.BaseUri, "test-key");
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri, puzzleQueueMin: 3), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
            Assert.That(result.Detail, Is.EqualTo("queue empty: tomorrow recycles an old puzzle"));
        });
    }

    [Test]
    public async Task Fails_when_the_header_is_missing()
    {
        await using var stub = new StubSite();
        stub.Map("/rest/v1/puzzles", ctx => ctx.Response.WriteAsync("[]"));
        await stub.StartAsync();

        using var provider = BuildWithSource(stub.BaseUri, "test-key");
        var result = await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri, puzzleQueueMin: 3), Window, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CheckStatus.Fail));
    }

    [Test]
    public async Task Errors_when_puzzle_source_is_not_configured()
    {
        using var provider = ProbeHost.Build(); // no PuzzleSource section at all

        var result = await Probe(provider).RunAsync(TestGames.Crosswords("http://x.invalid", puzzleQueueMin: 3), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Error));
            Assert.That(result.Detail, Is.EqualTo("PUZZLE_DB_URL / PUZZLE_DB_KEY not set"));
        });
    }

    [Test]
    public async Task Request_carries_the_expected_postgrest_headers()
    {
        string? apiKey = null;
        string? authorization = null;
        string? prefer = null;

        await using var stub = new StubSite();
        stub.Map("/rest/v1/puzzles", ctx =>
        {
            apiKey = ctx.Request.Headers["apikey"];
            authorization = ctx.Request.Headers["Authorization"];
            prefer = ctx.Request.Headers["Prefer"];
            ctx.Response.Headers["Content-Range"] = "0-0/5";
            return ctx.Response.WriteAsync("[]");
        });
        await stub.StartAsync();

        using var provider = BuildWithSource(stub.BaseUri, "super-secret-key");
        await Probe(provider).RunAsync(TestGames.Crosswords(stub.BaseUri, puzzleQueueMin: 3), Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(apiKey, Is.EqualTo("super-secret-key"));
            Assert.That(authorization, Is.EqualTo("Bearer super-secret-key"));
            Assert.That(prefer, Is.EqualTo("count=exact"));
        });
    }

    [Test]
    public void Applies_only_when_a_queue_minimum_is_configured()
    {
        using var provider = ProbeHost.Build();
        var probe = Probe(provider);

        Assert.Multiple(() =>
        {
            Assert.That(probe.AppliesTo(TestGames.Crosswords("http://x.invalid")), Is.True);
            Assert.That(probe.AppliesTo(TestGames.Crosswords("http://x.invalid", puzzleQueueMin: null)), Is.False);
            Assert.That(probe.AppliesTo(TestGames.MakeMeRed("http://x.invalid")), Is.False);
        });
    }
}
