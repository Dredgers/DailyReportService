using DailyReport.Core.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DailyReport.Infrastructure.State;

public interface IRunStore
{
    Task<bool> WasSentAsync(DateOnly reportDate, CancellationToken cancellationToken);

    Task RecordAsync(ReportRun run, CancellationToken cancellationToken);

    /// <summary>When the most recent Sent or DryRun finished, or null if never.</summary>
    Task<DateTimeOffset?> LastSuccessAsync(CancellationToken cancellationToken);

    /// <summary>Report date of the most recent Sent run, or null.</summary>
    Task<DateOnly?> LastSentReportDateAsync(CancellationToken cancellationToken);
}

/// <summary>SQLite-backed store. The schema is created on first use; there is nothing to migrate and no one else reads it.</summary>
public sealed class SqliteRunStore(IDbContextFactory<ReportStateDbContext> factory) : IRunStore
{
    private readonly SemaphoreSlim created = new(1, 1);
    private bool ensured;

    public async Task<bool> WasSentAsync(DateOnly reportDate, CancellationToken cancellationToken)
    {
        await using var ctx = await OpenAsync(cancellationToken);
        var key = Key(reportDate);
        return await ctx.Runs.AnyAsync(r => r.ReportDate == key && r.Status == RunStatus.Sent, cancellationToken);
    }

    public async Task RecordAsync(ReportRun run, CancellationToken cancellationToken)
    {
        await using var ctx = await OpenAsync(cancellationToken);
        ctx.Runs.Add(run);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> LastSuccessAsync(CancellationToken cancellationToken)
    {
        await using var ctx = await OpenAsync(cancellationToken);
        var last = await ctx.Runs
            .Where(r => r.Status == RunStatus.Sent || r.Status == RunStatus.DryRun)
            .OrderByDescending(r => r.FinishedAtUnixMs)
            .Select(r => (long?)r.FinishedAtUnixMs)
            .FirstOrDefaultAsync(cancellationToken);

        return last is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(last.Value);
    }

    public async Task<DateOnly?> LastSentReportDateAsync(CancellationToken cancellationToken)
    {
        await using var ctx = await OpenAsync(cancellationToken);
        var last = await ctx.Runs
            .Where(r => r.Status == RunStatus.Sent)
            .OrderByDescending(r => r.ReportDate)
            .Select(r => r.ReportDate)
            .FirstOrDefaultAsync(cancellationToken);

        return last is null ? null : DateOnly.ParseExact(last, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string Key(DateOnly reportDate) => reportDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<ReportStateDbContext> OpenAsync(CancellationToken cancellationToken)
    {
        var ctx = await factory.CreateDbContextAsync(cancellationToken);
        if (!ensured)
        {
            await created.WaitAsync(cancellationToken);
            try
            {
                if (!ensured)
                {
                    await ctx.Database.EnsureCreatedAsync(cancellationToken);
                    ensured = true;
                }
            }
            finally
            {
                created.Release();
            }
        }

        return ctx;
    }
}

public static class StateServiceCollectionExtensions
{
    public const string FileName = "dailyreport.db";

    public static string DatabasePath(ReportOptions options) => Path.Combine(Path.GetFullPath(options.StateDirectory), FileName);

    public static IServiceCollection AddReportState(this IServiceCollection services)
    {
        services.AddDbContextFactory<ReportStateDbContext>((sp, options) =>
        {
            var report = sp.GetRequiredService<IOptions<ReportOptions>>().Value;
            Directory.CreateDirectory(Path.GetFullPath(report.StateDirectory));
            options.UseSqlite($"Data Source={DatabasePath(report)}");
        });
        services.AddSingleton<IRunStore, SqliteRunStore>();
        return services;
    }
}
