using DailyReport.Infrastructure.State;
using Microsoft.EntityFrameworkCore;

namespace DailyReport.Tests.State;

public sealed class TempStateFactory : IDbContextFactory<ReportStateDbContext>, IDisposable
{
    public string Directory { get; } = Path.Combine(TestContext.CurrentContext.WorkDirectory, "state-" + Guid.NewGuid().ToString("N"));

    public TempStateFactory() => System.IO.Directory.CreateDirectory(Directory);

    public ReportStateDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ReportStateDbContext>().UseSqlite($"Data Source={Path.Combine(Directory, StateServiceCollectionExtensions.FileName)}").Options);

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // SQLite may still hold the file for a moment; the work directory is scratch anyway.
        }
    }
}

public sealed class SqliteRunStoreTests
{
    private static ReportRun Run(string date, RunStatus status, long finishedMs) => new()
    {
        ReportDate = date, Trigger = "test", StartedAtUnixMs = finishedMs - 1000, FinishedAtUnixMs = finishedMs, Status = status, Subject = "s",
    };

    [Test]
    public async Task Only_sent_runs_count_as_sent_but_dry_runs_count_as_success()
    {
        using var factory = new TempStateFactory();
        var store = new SqliteRunStore(factory);
        var day = new DateOnly(2026, 9, 11);

        Assert.That(await store.WasSentAsync(day, default), Is.False, "fresh store");

        await store.RecordAsync(Run("2026-09-10", RunStatus.Sent, 1_000), default);
        await store.RecordAsync(Run("2026-09-11", RunStatus.Failed, 2_000), default);
        await store.RecordAsync(Run("2026-09-11", RunStatus.DryRun, 3_000), default);

        Assert.Multiple(async () =>
        {
            Assert.That(await store.WasSentAsync(day, default), Is.False, "failed and dry runs are not sends");
            Assert.That(await store.WasSentAsync(day.AddDays(-1), default), Is.True);
            Assert.That(await store.LastSuccessAsync(default), Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(3_000)));
            Assert.That(await store.LastSentReportDateAsync(default), Is.EqualTo(day.AddDays(-1)));
        });

        await store.RecordAsync(Run("2026-09-11", RunStatus.Sent, 4_000), default);

        Assert.Multiple(async () =>
        {
            Assert.That(await store.WasSentAsync(day, default), Is.True);
            Assert.That(await store.LastSentReportDateAsync(default), Is.EqualTo(day));
        });
    }

    [Test]
    public async Task Empty_store_has_no_success()
    {
        using var factory = new TempStateFactory();
        var store = new SqliteRunStore(factory);

        Assert.Multiple(async () =>
        {
            Assert.That(await store.LastSuccessAsync(default), Is.Null);
            Assert.That(await store.LastSentReportDateAsync(default), Is.Null);
        });
    }
}
