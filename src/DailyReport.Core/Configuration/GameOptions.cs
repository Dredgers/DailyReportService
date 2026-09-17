namespace DailyReport.Core.Configuration;

/// <summary>Root wrapper so the top-level "Games" array binds and validates as one unit.</summary>
public sealed class GamesOptions
{
    public List<GameOptions> Games { get; set; } = [];
}

/// <summary>
/// One configured game. The instance is config; the queries are a provider class per <see cref="MetricsProvider"/>.
/// Adding a game is one of these plus, if its schema is new, a provider.
/// </summary>
public sealed class GameOptions
{
    /// <summary>Stable slug used in metric keys, logs and the state store. Lowercase, no spaces.</summary>
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Production origin, https, no trailing path. Probes are relative to it.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The zone this game's "day" is counted in. Crosswords: Europe/Copenhagen. Make Me Red: UTC.</summary>
    public string DayTimeZone { get; set; } = "";

    public MetricsOptions Metrics { get; set; } = new();

    public GoatCounterOptions? GoatCounter { get; set; }

    public ProbeOptions Probes { get; set; } = new();

    public Uri BaseUri => new(BaseUrl, UriKind.Absolute);

    public TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(DayTimeZone);
}

public enum MetricsProvider
{
    None = 0,
    Crosswords,
    MakeMeRed,
}

public enum ArrivalsSource
{
    None = 0,

    /// <summary>Crosswords' own <c>public.events</c> table: landing_view + invite_joined, one row per page load.</summary>
    EventsTable,

    /// <summary>GoatCounter visits for the site named in <see cref="GameOptions.GoatCounter"/>.</summary>
    GoatCounter,
}

public sealed class MetricsOptions
{
    public MetricsProvider Provider { get; set; } = MetricsProvider.None;

    public ArrivalsSource Arrivals { get; set; } = ArrivalsSource.None;
}

public sealed class GoatCounterOptions
{
    /// <summary>The subdomain: "makemered" for makemered.goatcounter.com.</summary>
    public string Site { get; set; } = "";

    /// <summary>Name of the environment variable holding the read-statistics API token.</summary>
    public string TokenEnv { get; set; } = "";
}

public enum PuzzlePublishedProbe
{
    None = 0,

    /// <summary>GET /api/puzzles; currentPublishedAt must fall inside today's game day.</summary>
    CrosswordsApiPuzzles,

    /// <summary>GET /api/puzzle/today; date must equal UTC today and puzzleNumber must match the epoch arithmetic.</summary>
    MakeMeRedApiToday,
}

public enum ProbeCadence
{
    Daily = 0,
    Weekly,
}

/// <summary>
/// The name of every check the report can run, in one place, so a game's <see cref="ProbeOptions.Paused"/>
/// entry can be validated at startup instead of failing silently as a check that never stopped going red.
/// </summary>
public static class ProbeNames
{
    public const string SiteUp = "Site up";
    public const string PuzzlePublished = "Puzzle published";
    public const string PuzzleQueue = "Puzzle queue";

    public static readonly IReadOnlyList<string> All = [SiteUp, PuzzlePublished, PuzzleQueue];
}

public sealed class ProbeOptions
{
    public PuzzlePublishedProbe PuzzlePublished { get; set; } = PuzzlePublishedProbe.None;

    /// <summary>Crosswords only: warn when approved-and-unused puzzles drop below this. Null disables the probe.</summary>
    public int? PuzzleQueueMin { get; set; }

    /// <summary>Probe the WebSocket with a side-effect-free queryRoom. Crosswords only.</summary>
    public bool WebSocket { get; set; }

    public OgImageOptions? OgImage { get; set; }

    /// <summary>
    /// Checks that are expected to fail because of a deliberate decision, mapped to the reason. A paused check
    /// still appears in the report, as Skipped with its reason, rather than red: a red line every morning that
    /// you are meant to ignore is worse than no line at all, because it teaches you to ignore red.
    /// Keys are check names from <see cref="ProbeNames"/> and are validated on startup. Remove the entry to resume.
    /// </summary>
    public Dictionary<string, string> Paused { get; set; } = [];

    /// <summary>The reason this check is paused, or null if it should run. Case-insensitive, since config keys are hand-typed.</summary>
    public string? PauseReasonFor(string checkName) =>
        Paused.FirstOrDefault(kv => string.Equals(kv.Key, checkName, StringComparison.OrdinalIgnoreCase)).Value;
}

public sealed class OgImageOptions
{
    public ProbeCadence Cadence { get; set; } = ProbeCadence.Weekly;
}
