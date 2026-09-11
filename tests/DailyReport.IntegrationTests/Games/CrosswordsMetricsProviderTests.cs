using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.Games.Crosswords;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyReport.IntegrationTests.Games;

public sealed class CrosswordsMetricsProviderTests
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    // The report goes out on the morning of 2026-09-11; the game day is Thursday 2026-09-10 (CEST, UTC+2).
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), Copenhagen);

    private static readonly GameOptions Game = new()
    {
        Key = "crosswords", Name = "Competitive Crosswords", BaseUrl = "https://www.competitivecrosswords.com", DayTimeZone = "Europe/Copenhagen",
        Metrics = new MetricsOptions { Provider = MetricsProvider.Crosswords, Arrivals = ArrivalsSource.EventsTable },
    };

    private TestData data = null!;

    [SetUp]
    public async Task SetUp()
    {
        data = new TestData();
        await data.ResetAsync();
    }

    [TearDown]
    public async Task TearDown() => await data.DisposeAsync();

    private static DateTimeOffset Utc(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    [Test]
    public async Task Counts_days_in_copenhagen_and_applies_the_completion_rules()
    {
        // Arrivals on the game day: 23:59 local on the 10th and 00:00 local on the 10th both count; 00:00 local on the 11th does not.
        await data.AddEventAsync("landing_view", Utc(9, 10, 21, 59));
        await data.AddEventAsync("invite_joined", Utc(9, 9, 22, 0));
        await data.AddEventAsync("landing_view", Utc(9, 10, 22, 0));
        await data.AddEventAsync("room_created", Utc(9, 10, 12));
        await data.AddEventAsync("landing_view", Utc(9, 9, 12)); // day before
        await data.AddEventAsync("game_finished", Utc(9, 10, 12), mode: "solo");
        await data.AddEventAsync("game_finished", Utc(9, 10, 13), mode: "competitive", playerCount: 2);
        await data.AddEventAsync("game_finished", Utc(9, 9, 12), mode: "solo");

        var a = await data.CreateUserAsync();
        var b = await data.CreateUserAsync();
        var c = await data.CreateUserAsync();
        var puzzle = await data.CreatePuzzleAsync();

        // Cohort day for 09-10 is 09-03: A and B completed genuinely then.
        await data.AddGameResultAsync(a, puzzle, "solo", Utc(9, 3, 10));
        await data.AddGameResultAsync(b, puzzle, "solo", Utc(9, 3, 11));
        // Game day: A genuine (returned), B a give-up (not a completion, not a return), C a replay (still a completion).
        await data.AddGameResultAsync(a, puzzle, "solo", Utc(9, 10, 10));
        await data.AddGameResultAsync(b, puzzle, "solo", Utc(9, 10, 11), durationS: null);
        await data.AddGameResultAsync(c, puzzle, "competitive", Utc(9, 10, 12), durationS: 200, ranked: false, playerCount: 2);

        var provider = new CrosswordsMetricsProvider(new ReportRoDatabase(), new FakeGoatCounterClient { Throw = new InvalidOperationException("must not be called") }, NullLogger<CrosswordsMetricsProvider>.Instance);

        var result = await provider.CollectAsync(Game, Window, CancellationToken.None);
        var series = result.Series.ToDictionary(s => s.Definition.Key);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failures, Is.Empty);
            Assert.That(series.Keys, Is.EquivalentTo(new[] { "crosswords.arrivals", "crosswords.completions", "crosswords.completions.signed_in", "crosswords.return_rate", "crosswords.return_cohort" }));
            foreach (var s in series.Values)
            {
                Assert.That(s.ByDay.Keys, Is.EquivalentTo(Window.SeriesDays), s.Definition.Key);
            }

            Assert.That(series["crosswords.arrivals"].On(Window.GameDay), Is.EqualTo(2), "arrivals on the game day");
            Assert.That(series["crosswords.arrivals"].On(Window.DayBefore), Is.EqualTo(1), "arrivals the day before");
            Assert.That(series["crosswords.arrivals"].On(Window.GameDay.AddDays(-5)), Is.EqualTo(0), "a quiet day is zero, not missing");
            Assert.That(series["crosswords.completions"].On(Window.GameDay), Is.EqualTo(2));
            Assert.That(series["crosswords.completions"].On(Window.DayBefore), Is.EqualTo(1));
            Assert.That(series["crosswords.completions.signed_in"].On(Window.GameDay), Is.EqualTo(2), "A genuine + C replay; B's give-up excluded");
            Assert.That(series["crosswords.completions.signed_in"].Definition.Lane, Is.EqualTo(Lane.SignedIn));
            Assert.That(series["crosswords.return_cohort"].On(Window.GameDay), Is.EqualTo(2));
            Assert.That(series["crosswords.return_rate"].On(Window.GameDay), Is.EqualTo(50));
            Assert.That(series["crosswords.return_rate"].On(Window.DayBefore), Is.Null, "no cohort on 09-02 means no rate");
        });
    }

    [Test]
    public async Task Goatcounter_arrivals_replace_the_events_line_when_configured_and_fail_softly()
    {
        var game = new GameOptions
        {
            Key = "crosswords", Name = Game.Name, BaseUrl = Game.BaseUrl, DayTimeZone = Game.DayTimeZone,
            Metrics = new MetricsOptions { Provider = MetricsProvider.Crosswords, Arrivals = ArrivalsSource.GoatCounter },
            GoatCounter = new GoatCounterOptions { Site = "competitivecrosswords", TokenEnv = "GOATCOUNTER_CC_TOKEN" },
        };

        var fake = new FakeGoatCounterClient();
        fake.Visits[Window.GameDay] = 77;
        var ok = await new CrosswordsMetricsProvider(new ReportRoDatabase(), fake, NullLogger<CrosswordsMetricsProvider>.Instance).CollectAsync(game, Window, CancellationToken.None);

        var down = await new CrosswordsMetricsProvider(new ReportRoDatabase(), FakeGoatCounterClient.MissingToken("GOATCOUNTER_CC_TOKEN"), NullLogger<CrosswordsMetricsProvider>.Instance).CollectAsync(game, Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(ok.Series.Single(s => s.Definition.Key == "crosswords.arrivals").On(Window.GameDay), Is.EqualTo(77));
            Assert.That(ok.Series.Single(s => s.Definition.Key == "crosswords.arrivals").Definition.Note, Does.Contain("GoatCounter"));

            Assert.That(down.Failures, Has.Count.EqualTo(1));
            Assert.That(down.Failures[0].Section, Does.Contain("GoatCounter"));
            Assert.That(down.Failures[0].Summary, Does.Contain("GOATCOUNTER_CC_TOKEN"));
            Assert.That(down.Series.Select(s => s.Definition.Key), Does.Not.Contain("crosswords.arrivals"));
            Assert.That(down.Series.Select(s => s.Definition.Key), Does.Contain("crosswords.completions.signed_in"), "the database half still reports");
        });
    }
}
