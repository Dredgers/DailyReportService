using System.Globalization;

namespace DailyReport.Core.Configuration;

/// <summary>
/// Pure validation so Core stays package-free; the Worker wraps these into IValidateOptions.
/// Every rule here is a startup failure on purpose: a misconfigured game must not silently vanish from the report.
/// </summary>
public static class OptionsValidation
{
    public static IReadOnlyList<string> Validate(ReportOptions o)
    {
        var errors = new List<string>();

        if (!TimeOnly.TryParseExact(o.SendAtLocalTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            errors.Add($"Report:SendAtLocalTime '{o.SendAtLocalTime}' is not HH:mm.");
        }

        if (!ZoneExists(o.TimeZone))
        {
            errors.Add($"Report:TimeZone '{o.TimeZone}' is not a known IANA zone on this machine.");
        }

        if (o.To.Length == 0 || o.To.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Report:To needs at least one recipient (set REPORT_TO in production).");
        }

        if (string.IsNullOrWhiteSpace(o.From) || !o.From.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("Report:From must be an email address (set REPORT_FROM in production).");
        }

        if (string.IsNullOrWhiteSpace(o.StateDirectory))
        {
            errors.Add("Report:StateDirectory is required.");
        }

        if (o.SectionTimeoutSeconds <= 0 || o.ProbeTimeoutSeconds <= 0)
        {
            errors.Add("Report timeouts must be positive.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(GamesOptions o)
    {
        var errors = new List<string>();

        if (o.Games.Count == 0)
        {
            errors.Add("Games: at least one game must be configured.");
            return errors;
        }

        var duplicateKeys = o.Games.GroupBy(g => g.Key, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var key in duplicateKeys)
        {
            errors.Add($"Games: key '{key}' is used more than once.");
        }

        foreach (var g in o.Games)
        {
            var where = $"Games[{g.Key}]";

            if (string.IsNullOrWhiteSpace(g.Key) || g.Key.Any(c => !(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')))
            {
                errors.Add($"{where}: Key must be a lowercase slug (a-z, 0-9, '-').");
            }

            if (string.IsNullOrWhiteSpace(g.Name))
            {
                errors.Add($"{where}: Name is required.");
            }

            if (!Uri.TryCreate(g.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/")
            {
                errors.Add($"{where}: BaseUrl must be an absolute https origin with no path.");
            }

            if (!ZoneExists(g.DayTimeZone))
            {
                errors.Add($"{where}: DayTimeZone '{g.DayTimeZone}' is not a known IANA zone on this machine.");
            }

            if (g.Metrics.Provider == MetricsProvider.None)
            {
                errors.Add($"{where}: Metrics:Provider is required.");
            }

            if (g.Metrics.Arrivals == ArrivalsSource.GoatCounter && g.GoatCounter is null)
            {
                errors.Add($"{where}: Metrics:Arrivals is GoatCounter but no GoatCounter section is configured.");
            }

            if (g.Metrics.Arrivals == ArrivalsSource.EventsTable && g.Metrics.Provider != MetricsProvider.Crosswords)
            {
                errors.Add($"{where}: only the Crosswords provider has an events table.");
            }

            if (g.GoatCounter is { } gc && (string.IsNullOrWhiteSpace(gc.Site) || string.IsNullOrWhiteSpace(gc.TokenEnv)))
            {
                errors.Add($"{where}: GoatCounter needs both Site and TokenEnv.");
            }

            if (g.Probes.PuzzleQueueMin is < 0)
            {
                errors.Add($"{where}: Probes:PuzzleQueueMin cannot be negative.");
            }

            if (g.Probes.PuzzleQueueMin is not null && g.Metrics.Provider != MetricsProvider.Crosswords)
            {
                errors.Add($"{where}: only Crosswords has a puzzle queue.");
            }

            foreach (var (checkName, reason) in g.Probes.Paused)
            {
                if (!ProbeNames.All.Contains(checkName, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"{where}: Probes:Paused names no such check '{checkName}'. Known: {string.Join(", ", ProbeNames.All)}.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    errors.Add($"{where}: Probes:Paused['{checkName}'] needs a reason; it is printed in the report so you remember why.");
                }
            }
        }

        return errors;
    }

    private static bool ZoneExists(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
