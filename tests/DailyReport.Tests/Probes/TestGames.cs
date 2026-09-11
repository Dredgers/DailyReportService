using DailyReport.Core.Configuration;

namespace DailyReport.Tests.Probes;

/// <summary>
/// Minimal <see cref="GameOptions"/> shaped like the two real games in appsettings.json, pointed at a
/// <see cref="StubSite"/> instead of the real origin. Options validation does not run on these directly, so
/// an http BaseUrl is fine here even though production requires https.
/// </summary>
internal static class TestGames
{
    public static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public static GameOptions Crosswords(string baseUri, int? puzzleQueueMin = 3) => new()
    {
        Key = "crosswords",
        Name = "Competitive Crosswords",
        BaseUrl = baseUri,
        DayTimeZone = "Europe/Copenhagen",
        Metrics = new MetricsOptions { Provider = MetricsProvider.Crosswords, Arrivals = ArrivalsSource.EventsTable },
        Probes = new ProbeOptions { PuzzlePublished = PuzzlePublishedProbe.CrosswordsApiPuzzles, PuzzleQueueMin = puzzleQueueMin, WebSocket = true },
    };

    public static GameOptions MakeMeRed(string baseUri) => new()
    {
        Key = "makemered",
        Name = "Make Me Red",
        BaseUrl = baseUri,
        DayTimeZone = "UTC",
        Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed, Arrivals = ArrivalsSource.GoatCounter },
        GoatCounter = new GoatCounterOptions { Site = "makemered", TokenEnv = "GOATCOUNTER_MMR_TOKEN" },
        Probes = new ProbeOptions { PuzzlePublished = PuzzlePublishedProbe.MakeMeRedApiToday },
    };
}
