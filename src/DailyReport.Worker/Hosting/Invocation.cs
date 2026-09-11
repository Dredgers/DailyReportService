using System.Globalization;

namespace DailyReport.Worker.Hosting;

/// <summary>Parsed command line. Kept tiny on purpose; anything richer belongs in configuration.</summary>
public sealed record Invocation(bool Once, bool DryRun, DateOnly? Date, bool Force, bool HealthCheck)
{
    public static Invocation Parse(string[] args)
    {
        var once = args.Contains("--once", StringComparer.Ordinal);
        var dryRun = args.Contains("--dry-run", StringComparer.Ordinal);
        var force = args.Contains("--force", StringComparer.Ordinal);
        var healthCheck = args.Contains("--healthcheck", StringComparer.Ordinal);
        DateOnly? date = null;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--date=", StringComparison.Ordinal))
            {
                date = DateOnly.ParseExact(arg["--date=".Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }

        if ((dryRun || date is not null || force) && !once)
        {
            throw new ArgumentException("--dry-run, --date and --force only make sense with --once.");
        }

        if (healthCheck && (once || dryRun))
        {
            throw new ArgumentException("--healthcheck stands alone.");
        }

        return new Invocation(once, dryRun, date, force, healthCheck);
    }
}
