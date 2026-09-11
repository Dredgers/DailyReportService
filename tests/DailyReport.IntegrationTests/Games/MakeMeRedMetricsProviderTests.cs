using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.Games.MakeMeRed;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyReport.IntegrationTests.Games;

public sealed class MakeMeRedMetricsProviderTests
{
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), TimeZoneInfo.Utc);

    private static readonly GameOptions Game = new()
    {
        Key = "makemered", Name = "Make Me Red", BaseUrl = "https://makeme.red", DayTimeZone = "UTC",
        Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed, Arrivals = ArrivalsSource.GoatCounter },
        GoatCounter = new GoatCounterOptions { Site = "makemered", TokenEnv = "GOATCOUNTER_MMR_TOKEN" },
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

    [Test]
    public async Task Guests_come_from_goatcounter_and_signed_in_from_mmr_results()
    {
        var day = Window.GameDay;
        var fake = new FakeGoatCounterClient();
        fake.Visits[day] = 120;
        fake.Visits[Window.DayBefore] = 100;
        fake.Events[("solve", day)] = 40;
        fake.Events[("solve", Window.DayBefore)] = 30;
        fake.Events[("solve-four", day)] = 5;
        fake.Events[("share", day)] = 3;

        var a = await data.CreateUserAsync();
        var b = await data.CreateUserAsync();
        var c = await data.CreateUserAsync();
        var noon = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

        // Cohort day 09-03: A and B completed.
        await data.AddMmrResultAsync(a, day.AddDays(-7), completedAt: noon.AddDays(-7));
        await data.AddMmrResultAsync(b, day.AddDays(-7), completedAt: noon.AddDays(-7));
        // Game day: A completes three and four, B started but did not finish, C completes five.
        await data.AddMmrResultAsync(a, day, "three", completedAt: noon);
        await data.AddMmrResultAsync(a, day, "four", completedAt: noon);
        await data.AddMmrResultAsync(b, day, "three", completedAt: null, moves: null, timeMs: null, verified: false, attempts: null);
        await data.AddMmrResultAsync(c, day, "five", completedAt: noon);

        var result = await new MakeMeRedMetricsProvider(new ReportRoDatabase(), fake, NullLogger<MakeMeRedMetricsProvider>.Instance).CollectAsync(Game, Window, CancellationToken.None);
        var series = result.Series.ToDictionary(s => s.Definition.Key);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failures, Is.Empty);
            foreach (var s in series.Values)
            {
                Assert.That(s.ByDay.Keys, Is.EquivalentTo(Window.SeriesDays), s.Definition.Key);
            }

            Assert.That(series["makemered.arrivals"].On(day), Is.EqualTo(120));
            Assert.That(series["makemered.arrivals"].On(Window.DayBefore), Is.EqualTo(100));
            Assert.That(series["makemered.completions.three"].On(day), Is.EqualTo(40));
            Assert.That(series["makemered.completions.three"].On(Window.DayBefore), Is.EqualTo(30));
            Assert.That(series["makemered.completions.four"].On(day), Is.EqualTo(5));
            Assert.That(series["makemered.completions.five"].On(day), Is.EqualTo(0), "no events means zero, not missing");
            Assert.That(series["makemered.shares"].On(day), Is.EqualTo(3));
            Assert.That(series["makemered.completions.three.signed_in"].On(day), Is.EqualTo(1), "A; B never completed");
            Assert.That(series["makemered.completions.four.signed_in"].On(day), Is.EqualTo(1));
            Assert.That(series["makemered.completions.five.signed_in"].On(day), Is.EqualTo(1));
            Assert.That(series["makemered.completions.five.signed_in"].Definition.Lane, Is.EqualTo(Lane.SignedIn));
            Assert.That(series["makemered.return_cohort"].On(day), Is.EqualTo(2));
            Assert.That(series["makemered.return_rate"].On(day), Is.EqualTo(50), "A returned, B did not");
        });
    }

    [Test]
    public async Task A_missing_token_fails_the_goatcounter_half_and_keeps_the_database_half()
    {
        var a = await data.CreateUserAsync();
        await data.AddMmrResultAsync(a, Window.GameDay, completedAt: new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));

        var result = await new MakeMeRedMetricsProvider(new ReportRoDatabase(), FakeGoatCounterClient.MissingToken(), NullLogger<MakeMeRedMetricsProvider>.Instance).CollectAsync(Game, Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failures.Select(f => f.Section), Is.EqualTo(new[] { "GoatCounter" }));
            Assert.That(result.Failures[0].Summary, Does.Contain("GOATCOUNTER_MMR_TOKEN"));
            Assert.That(result.Series.Select(s => s.Definition.Key), Does.Not.Contain("makemered.arrivals"));
            Assert.That(result.Series.Single(s => s.Definition.Key == "makemered.completions.three.signed_in").On(Window.GameDay), Is.EqualTo(1));
        });
    }
}

public sealed class MakeMeRedPartialFailureTests
{
    private static readonly ReportWindow Window = new(new DateOnly(2026, 9, 11), TimeZoneInfo.Utc);

    [Test]
    public async Task An_unreachable_database_keeps_the_goatcounter_lines_and_reports_both_halves_honestly()
    {
        var game = new GameOptions
        {
            Key = "makemered", Name = "Make Me Red", BaseUrl = "https://makeme.red", DayTimeZone = "UTC",
            Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed, Arrivals = ArrivalsSource.GoatCounter },
            GoatCounter = new GoatCounterOptions { Site = "makemered", TokenEnv = "GOATCOUNTER_MMR_TOKEN" },
        };
        var fake = new FakeGoatCounterClient();
        fake.Visits[Window.GameDay] = 9;

        var result = await new MakeMeRedMetricsProvider(new UnreachableDatabase(), fake, NullLogger<MakeMeRedMetricsProvider>.Instance).CollectAsync(game, Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failures.Select(f => f.Section), Is.EqualTo(new[] { "Database" }));
            Assert.That(result.Failures[0].Summary, Does.Contain("REPORT_DB_CONNECTION"));
            Assert.That(result.Series.Single(s => s.Definition.Key == "makemered.arrivals").On(Window.GameDay), Is.EqualTo(9));
            Assert.That(result.Series.Select(s => s.Definition.Key), Does.Not.Contain("makemered.return_rate"));
        });

        var both = await new MakeMeRedMetricsProvider(new UnreachableDatabase(), FakeGoatCounterClient.MissingToken(), NullLogger<MakeMeRedMetricsProvider>.Instance).CollectAsync(game, Window, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(both.Series, Is.Empty);
            Assert.That(both.Failures.Select(f => f.Section), Is.EquivalentTo(new[] { "GoatCounter", "Database" }));
        });
    }
}
