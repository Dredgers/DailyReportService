namespace DailyReport.Core.Configuration;

/// <summary>Crosswords' separate puzzle-library Supabase project, read over PostgREST for the low-queue probe only.</summary>
public sealed class PuzzleSourceOptions
{
    public const string SectionName = "PuzzleSource";

    /// <summary>The project URL, e.g. https://xxxx.supabase.co (PUZZLE_DB_URL).</summary>
    public string Url { get; set; } = "";

    /// <summary>The API key the crosswords server already uses (PUZZLE_DB_KEY).</summary>
    public string Key { get; set; } = "";
}

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = "";

    public string BaseUrl { get; set; } = "https://api.resend.com";
}

public sealed class GoatCounterClientOptions
{
    public const string SectionName = "GoatCounterClient";

    /// <summary>Host pattern; {site} is replaced with the configured site name.</summary>
    public string BaseUrlPattern { get; set; } = "https://{site}.goatcounter.com";
}
