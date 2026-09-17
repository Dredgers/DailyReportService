using DailyReport.Core.Configuration;
using DailyReport.Worker.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DailyReport.Tests.Configuration;

public sealed class OptionsValidationTests
{
    private static string WorkerDirectory =>
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "DailyReport.Worker"));

    [Test]
    public void Shipped_appsettings_binds_and_validates()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(WorkerDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Report:To:0"] = "someone@example.invalid",
                ["Report:From"] = "report@example.invalid",
            })
            .Build();

        var services = new ServiceCollection().AddDailyReport(configuration).AddLogging();
        using var provider = services.BuildServiceProvider();

        var report = provider.GetRequiredService<IOptions<ReportOptions>>().Value;
        var games = provider.GetRequiredService<IOptions<GamesOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(report.SendAt, Is.EqualTo(new TimeOnly(7, 0)));
            Assert.That(report.Zone.Id, Is.EqualTo("Europe/Copenhagen"));
            Assert.That(games.Games.Select(g => g.Key), Is.EqualTo(new[] { "crosswords", "makemered" }));
            Assert.That(games.Games[0].Metrics.Arrivals, Is.EqualTo(ArrivalsSource.EventsTable));
            Assert.That(games.Games[1].GoatCounter?.TokenEnv, Is.EqualTo("GOATCOUNTER_MMR_TOKEN"));
            Assert.That(games.Games[1].Zone.Id, Is.EqualTo("UTC"));
        });
    }

    [Test]
    public void Missing_recipients_is_a_startup_error()
    {
        var errors = OptionsValidation.Validate(new ReportOptions { From = "x@y.z", To = [] });
        Assert.That(errors, Has.Some.Contains("Report:To"));
    }

    [Test]
    public void Unknown_zone_is_a_startup_error()
    {
        var games = new GamesOptions
        {
            Games =
            [
                new GameOptions
                {
                    Key = "x", Name = "X", BaseUrl = "https://x.example", DayTimeZone = "Mars/Olympus",
                    Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed },
                },
            ],
        };

        var errors = OptionsValidation.Validate(games);
        Assert.That(errors, Has.Exactly(1).Contains("DayTimeZone"));
    }

    [Test]
    public void GoatCounter_arrivals_without_a_site_is_a_startup_error()
    {
        var games = new GamesOptions
        {
            Games =
            [
                new GameOptions
                {
                    Key = "x", Name = "X", BaseUrl = "https://x.example", DayTimeZone = "UTC",
                    Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed, Arrivals = ArrivalsSource.GoatCounter },
                },
            ],
        };

        var errors = OptionsValidation.Validate(games);
        Assert.That(errors, Has.Exactly(1).Contains("GoatCounter"));
    }

    [Test]
    public void A_paused_check_must_name_a_real_check_and_carry_a_reason()
    {
        static GamesOptions WithPause(string name, string reason) => new()
        {
            Games =
            [
                new GameOptions
                {
                    Key = "x", Name = "X", BaseUrl = "https://x.example", DayTimeZone = "UTC",
                    Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed },
                    Probes = new ProbeOptions { Paused = { [name] = reason } },
                },
            ],
        };

        Assert.Multiple(() =>
        {
            Assert.That(OptionsValidation.Validate(WithPause("Puzzle published", "supply on hold")), Is.Empty);
            Assert.That(OptionsValidation.Validate(WithPause("puzzle PUBLISHED", "supply on hold")), Is.Empty, "hand-typed keys are case-insensitive");
            Assert.That(OptionsValidation.Validate(WithPause("Puzle published", "typo")), Has.Exactly(1).Contains("no such check"));
            Assert.That(OptionsValidation.Validate(WithPause("Puzzle queue", "  ")), Has.Exactly(1).Contains("needs a reason"));
        });
    }

    [Test]
    public void Duplicate_keys_are_rejected()
    {
        GameOptions Game() => new()
        {
            Key = "same", Name = "Same", BaseUrl = "https://same.example", DayTimeZone = "UTC",
            Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed },
        };

        var errors = OptionsValidation.Validate(new GamesOptions { Games = [Game(), Game()] });
        Assert.That(errors, Has.Exactly(1).Contains("more than once"));
    }

    [Test]
    public void Secret_environment_variables_map_onto_configuration_keys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SecretEnvironmentVariables.Map["REPORT_DB_CONNECTION"], Is.EqualTo("ConnectionStrings:Games"));
            Assert.That(SecretEnvironmentVariables.Map["RESEND_API_KEY"], Is.EqualTo("Resend:ApiKey"));
        });
    }
}
