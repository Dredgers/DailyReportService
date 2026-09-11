using System.Globalization;

namespace DailyReport.Core.Configuration;

/// <summary>
/// Service-wide settings: when to send, in which zone, to whom. Bound from the "Report" section.
/// Secrets (API keys, connection strings) are never here; they arrive as environment variables.
/// </summary>
public sealed class ReportOptions
{
    public const string SectionName = "Report";

    /// <summary>Local wall-clock time of the daily send, "HH:mm", in <see cref="TimeZone"/>.</summary>
    public string SendAtLocalTime { get; set; } = "07:00";

    /// <summary>IANA zone the schedule runs in. The report date D is "today" in this zone.</summary>
    public string TimeZone { get; set; } = "Europe/Copenhagen";

    /// <summary>Recipients. Comes from REPORT_TO in production so no address lives in the repo.</summary>
    public string[] To { get; set; } = [];

    public string From { get; set; } = "";

    public string FromName { get; set; } = "Daily Report";

    /// <summary>Where the SQLite state and dry-run output live. A Docker volume in production.</summary>
    public string StateDirectory { get; set; } = "data";

    /// <summary>Upper bound for one metrics section (one game, one provider). Exceeding it marks the section failed.</summary>
    public int SectionTimeoutSeconds { get; set; } = 60;

    /// <summary>Upper bound for one health probe.</summary>
    public int ProbeTimeoutSeconds { get; set; } = 15;

    public TimeOnly SendAt => TimeOnly.ParseExact(SendAtLocalTime, "HH:mm", CultureInfo.InvariantCulture);

    public TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
}
