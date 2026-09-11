using System.Globalization;

namespace DailyReport.Worker.Hosting;

/// <summary>Parsed command line. Kept tiny on purpose; anything richer belongs in configuration.</summary>
public sealed record Invocation(bool Once, bool DryRun, DateOnly? Date, bool Force, bool HealthCheck)
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal) { "--once", "--dry-run", "--force", "--healthcheck" };

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
            else if (!Known.Contains(arg))
            {
                // A typo must never turn a dry run into a real send; refuse anything we do not recognise.
                throw new ArgumentException($"Unknown argument '{arg}'. Known: --once, --dry-run, --force, --date=YYYY-MM-DD, --healthcheck.");
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
