using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Probes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DailyReport.Tests.Probes;

/// <summary>Registration and AppliesTo checked against the games as they are actually configured for production.</summary>
public sealed class ProbeRegistrationTests
{
    private static string WorkerDirectory =>
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "DailyReport.Worker"));

    private static IReadOnlyList<GameOptions> LoadShippedGames()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(WorkerDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<GamesOptions>().Bind(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<GamesOptions>>().Value.Games;
    }

    [Test]
    public void AddHealthProbes_resolves_exactly_four_probes()
    {
        var services = new ServiceCollection();
        services.AddHealthProbes(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var probes = provider.GetServices<IHealthProbe>().ToList();

        Assert.That(probes, Has.Count.EqualTo(4));
    }

    [Test]
    public void Each_probes_AppliesTo_matches_the_shipped_game_configuration()
    {
        var games = LoadShippedGames();
        var crosswords = games.Single(g => g.Key == "crosswords");
        var makemered = games.Single(g => g.Key == "makemered");

        using var provider = ProbeHost.Build();
        var probes = provider.GetServices<IHealthProbe>().ToList();

        var healthz = probes.OfType<HealthzProbe>().Single();
        var crosswordsPublished = probes.OfType<CrosswordsPuzzlePublishedProbe>().Single();
        var makeMeRedPublished = probes.OfType<MakeMeRedPuzzlePublishedProbe>().Single();
        var queue = probes.OfType<PuzzleQueueProbe>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(healthz.AppliesTo(crosswords), Is.True);
            Assert.That(healthz.AppliesTo(makemered), Is.True);

            Assert.That(crosswordsPublished.AppliesTo(crosswords), Is.True);
            Assert.That(crosswordsPublished.AppliesTo(makemered), Is.False);

            Assert.That(makeMeRedPublished.AppliesTo(makemered), Is.True);
            Assert.That(makeMeRedPublished.AppliesTo(crosswords), Is.False);

            Assert.That(queue.AppliesTo(crosswords), Is.True);
            Assert.That(queue.AppliesTo(makemered), Is.False);
        });
    }
}
