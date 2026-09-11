namespace DailyReport.Worker.Hosting;

/// <summary>
/// The runbook names secrets flatly (RESEND_API_KEY, REPORT_TO, ...). This maps those names onto the
/// configuration keys the options classes bind to, so nobody has to remember the "Section__Key" spelling.
/// Only variables that are actually set are added; appsettings and the standard env provider still apply.
/// </summary>
public static class SecretEnvironmentVariables
{
    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["REPORT_DB_CONNECTION"] = "ConnectionStrings:Games",
        ["PUZZLE_DB_URL"] = "PuzzleSource:Url",
        ["PUZZLE_DB_KEY"] = "PuzzleSource:Key",
        ["RESEND_API_KEY"] = "Resend:ApiKey",
        ["REPORT_FROM"] = "Report:From",
        ["REPORT_FROM_NAME"] = "Report:FromName",
    };

    public static IConfigurationBuilder AddSecretEnvironmentVariables(this IConfigurationBuilder builder)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (env, key) in Map)
        {
            var value = Environment.GetEnvironmentVariable(env);
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
            }
        }

        // REPORT_TO is a comma-separated list; arrays bind from indexed keys.
        var to = Environment.GetEnvironmentVariable("REPORT_TO");
        if (!string.IsNullOrWhiteSpace(to))
        {
            var recipients = to.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < recipients.Length; i++)
            {
                values[$"Report:To:{i}"] = recipients[i];
            }
        }

        return values.Count == 0 ? builder : builder.AddInMemoryCollection(values);
    }
}
