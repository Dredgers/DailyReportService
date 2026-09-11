using DailyReport.Core.Model;

namespace DailyReport.Tests.Render;

/// <summary>
/// Four deterministic <see cref="Report"/>s shared by the renderer unit tests and the golden-file tests, so both
/// exercise exactly the same data. Built by constructing <see cref="Metric"/> records directly (not via
/// <see cref="DailyReport.Core.Composition.ReportComposer"/>) for full control over the delta shapes each
/// fixture is meant to cover; the composer's own behaviour is already covered by
/// <c>ReportComposerTests</c>.
/// </summary>
public static class ReportFixtures
{
    public static readonly DateOnly ReportDate = new(2026, 9, 11);
    public static readonly DateOnly GameDay = ReportDate.AddDays(-1); // 2026-09-10, a Thursday.
    public static readonly DateTimeOffset GeneratedAt = new(2026, 9, 11, 5, 0, 0, TimeSpan.Zero);

    private const string CrosswordsKey = "crosswords";
    private const string CrosswordsName = "Competitive Crosswords";
    private const string CrosswordsZone = "Europe/Copenhagen";

    private const string MakeMeRedKey = "makemered";
    private const string MakeMeRedName = "Make Me Red";
    private const string MakeMeRedZone = "UTC";

    private static readonly MetricDefinition CrosswordsArrivals =
        new("arrivals", "Traffic", "Arrivals", Lane.All, MetricUnit.Count, "Page loads, not unique visitors.");

    private static readonly MetricDefinition CrosswordsStarted =
        new("started", "Traffic", "Games started", Lane.All, MetricUnit.Count);

    private static readonly MetricDefinition CrosswordsCompletedAll =
        new("completed_all", "Completions", "Completed", Lane.All, MetricUnit.Count);

    private static readonly MetricDefinition CrosswordsCompletedSignedIn =
        new("completed_signedin", "Completions", "Completed", Lane.SignedIn, MetricUnit.Count);

    private static readonly MetricDefinition CrosswordsReturn =
        new("return_7d", "Return", "Returned within 7 days", Lane.SignedIn, MetricUnit.Percent, "Signed-in players only.");

    private static readonly MetricDefinition MakeMeRedArrivals =
        new("arrivals", "Traffic", "Arrivals", Lane.All, MetricUnit.Count);

    private static readonly MetricDefinition MakeMeRedCompletedAll =
        new("completed_all", "Completions", "Completed", Lane.All, MetricUnit.Count);

    private static readonly MetricDefinition MakeMeRedCompletedSignedIn =
        new("completed_signedin", "Completions", "Completed", Lane.SignedIn, MetricUnit.Count);

    private static readonly MetricDefinition MakeMeRedReturn =
        new("return_7d", "Return", "Returned within 7 days", Lane.SignedIn, MetricUnit.Percent, "Signed-in players only.");

    /// <summary>Both games, every group and lane, all checks passing but one, exactly one <see cref="CheckStatus.Skipped"/>.</summary>
    public static Report AllGreen()
    {
        var checks = new[]
        {
            CheckResult.Pass(CrosswordsKey, "Healthz", "200 OK", TimeSpan.FromMilliseconds(45)),
            CheckResult.Pass(CrosswordsKey, "Puzzle published", "Current", TimeSpan.FromMilliseconds(120)),
            CheckResult.Pass(CrosswordsKey, "Puzzle queue", "5 approved and unused", TimeSpan.FromMilliseconds(80)),
            CheckResult.Pass(MakeMeRedKey, "Healthz", "200 OK", TimeSpan.FromMilliseconds(30)),
            CheckResult.Pass(MakeMeRedKey, "Puzzle published", "Current", TimeSpan.FromMilliseconds(95)),
            CheckResult.Skipped(MakeMeRedKey, "WebSocket", "not applicable to this game"),
        };

        var games = new[]
        {
            new GameSection(CrosswordsKey, CrosswordsName, GameDay, CrosswordsZone, CrosswordsMetrics()),
            new GameSection(MakeMeRedKey, MakeMeRedName, GameDay, MakeMeRedZone, MakeMeRedMetrics()),
        };

        return new Report(ReportDate, GeneratedAt, checks, games, []);
    }

    /// <summary>Same metrics as <see cref="AllGreen"/>, but one failed check and one warning — testing that both the red banner and the amber block render together.</summary>
    public static Report OneFailedOneWarn()
    {
        var checks = new[]
        {
            CheckResult.Fail(CrosswordsKey, "Puzzle published", "currentPublishedAt is 2 days stale", TimeSpan.FromMilliseconds(110)),
            CheckResult.Warn(CrosswordsKey, "Puzzle queue", "2 approved and unused, below threshold of 3", TimeSpan.FromMilliseconds(75)),
            CheckResult.Pass(CrosswordsKey, "Healthz", "200 OK", TimeSpan.FromMilliseconds(45)),
            CheckResult.Pass(MakeMeRedKey, "Healthz", "200 OK", TimeSpan.FromMilliseconds(30)),
            CheckResult.Pass(MakeMeRedKey, "Puzzle published", "Current", TimeSpan.FromMilliseconds(95)),
            CheckResult.Skipped(MakeMeRedKey, "WebSocket", "not applicable to this game"),
        };

        var games = new[]
        {
            new GameSection(CrosswordsKey, CrosswordsName, GameDay, CrosswordsZone, CrosswordsMetrics()),
            new GameSection(MakeMeRedKey, MakeMeRedName, GameDay, MakeMeRedZone, MakeMeRedMetrics()),
        };

        return new Report(ReportDate, GeneratedAt, checks, games, []);
    }

    /// <summary>Make Me Red's whole metrics source is dead: zero metrics, one <see cref="SourceFailure"/>, red section instead of an empty table.</summary>
    public static Report SourceFailure()
    {
        var checks = new[]
        {
            CheckResult.Pass(CrosswordsKey, "Healthz", "200 OK", TimeSpan.FromMilliseconds(45)),
            CheckResult.Pass(CrosswordsKey, "Puzzle published", "Current", TimeSpan.FromMilliseconds(120)),
            CheckResult.Pass(CrosswordsKey, "Puzzle queue", "5 approved and unused", TimeSpan.FromMilliseconds(80)),
            CheckResult.Pass(MakeMeRedKey, "Healthz", "200 OK", TimeSpan.FromMilliseconds(30)),
        };

        var games = new[]
        {
            new GameSection(CrosswordsKey, CrosswordsName, GameDay, CrosswordsZone, CrosswordsMetrics()),
            new GameSection(MakeMeRedKey, MakeMeRedName, GameDay, MakeMeRedZone, []),
        };

        var failures = new[]
        {
            new SourceFailure(MakeMeRedKey, "Metrics", "GoatCounter: 401 Unauthorized"),
        };

        return new Report(ReportDate, GeneratedAt, checks, games, failures);
    }

    /// <summary>Both games, every metric null and every baseline empty — the "no data" path, never rendered as zero.</summary>
    public static Report NoData()
    {
        var checks = new[]
        {
            CheckResult.Pass(CrosswordsKey, "Healthz", "200 OK", TimeSpan.FromMilliseconds(45)),
            CheckResult.Pass(CrosswordsKey, "Puzzle published", "Current", TimeSpan.FromMilliseconds(120)),
            CheckResult.Pass(CrosswordsKey, "Puzzle queue", "5 approved and unused", TimeSpan.FromMilliseconds(80)),
            CheckResult.Pass(MakeMeRedKey, "Healthz", "200 OK", TimeSpan.FromMilliseconds(30)),
            CheckResult.Pass(MakeMeRedKey, "Puzzle published", "Current", TimeSpan.FromMilliseconds(95)),
            CheckResult.Skipped(MakeMeRedKey, "WebSocket", "not applicable to this game"),
        };

        var games = new[]
        {
            new GameSection(CrosswordsKey, CrosswordsName, GameDay, CrosswordsZone, NoDataMetrics(CrosswordsDefinitions)),
            new GameSection(MakeMeRedKey, MakeMeRedName, GameDay, MakeMeRedZone, NoDataMetrics(MakeMeRedDefinitions)),
        };

        return new Report(ReportDate, GeneratedAt, checks, games, []);
    }

    private static readonly IReadOnlyList<MetricDefinition> CrosswordsDefinitions =
    [
        CrosswordsArrivals, CrosswordsStarted, CrosswordsCompletedAll, CrosswordsCompletedSignedIn, CrosswordsReturn,
    ];

    private static readonly IReadOnlyList<MetricDefinition> MakeMeRedDefinitions =
    [
        MakeMeRedArrivals, MakeMeRedCompletedAll, MakeMeRedCompletedSignedIn, MakeMeRedReturn,
    ];

    private static List<Metric> CrosswordsMetrics() =>
    [
        // Positive delta on every comparison, all with a percent: ▲ throughout.
        new(CrosswordsArrivals, Value: 1200, DayBefore: 1000, SevenDayMean: 1000, BaselineDaysPresent: 7, SameWeekdayLastWeek: 1000),

        // Day-before delta is exactly zero ("= 0"); same-weekday is positive.
        new(CrosswordsStarted, Value: 300, DayBefore: 300, SevenDayMean: 300, BaselineDaysPresent: 7, SameWeekdayLastWeek: 250),

        // Day-before delta is negative (▼); seven-day baseline is present but averages to zero, so that delta has no percent.
        new(CrosswordsCompletedAll, Value: 150, DayBefore: 200, SevenDayMean: 0, BaselineDaysPresent: 7, SameWeekdayLastWeek: 150),

        // Only 3 of 7 baseline days had data: exercises the "3/7 days" tag.
        new(CrosswordsCompletedSignedIn, Value: 80, DayBefore: 70, SevenDayMean: 60, BaselineDaysPresent: 3, SameWeekdayLastWeek: 80),

        // Percent-unit metric: deltas render in percentage points ("pp"), mixing a positive, a larger positive and a negative.
        new(CrosswordsReturn, Value: 42.0, DayBefore: 40.0, SevenDayMean: 38.0, BaselineDaysPresent: 7, SameWeekdayLastWeek: 44.0),
    ];

    private static List<Metric> MakeMeRedMetrics() =>
    [
        new(MakeMeRedArrivals, Value: 500, DayBefore: 450, SevenDayMean: 450, BaselineDaysPresent: 7, SameWeekdayLastWeek: 500),
        new(MakeMeRedCompletedAll, Value: 90, DayBefore: 100, SevenDayMean: 95, BaselineDaysPresent: 7, SameWeekdayLastWeek: 90),
        new(MakeMeRedCompletedSignedIn, Value: 40, DayBefore: 35, SevenDayMean: 35, BaselineDaysPresent: 7, SameWeekdayLastWeek: 40),
        new(MakeMeRedReturn, Value: 25.0, DayBefore: 25.0, SevenDayMean: 20.0, BaselineDaysPresent: 7, SameWeekdayLastWeek: 30.0),
    ];

    private static List<Metric> NoDataMetrics(IReadOnlyList<MetricDefinition> definitions) =>
        definitions.Select(d => new Metric(d, Value: null, DayBefore: null, SevenDayMean: null, BaselineDaysPresent: 0, SameWeekdayLastWeek: null)).ToList();
}
