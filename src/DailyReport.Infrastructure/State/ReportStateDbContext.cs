using Microsoft.EntityFrameworkCore;

namespace DailyReport.Infrastructure.State;

/// <summary>The one database this service writes: a SQLite file under Report:StateDirectory.</summary>
public sealed class ReportStateDbContext(DbContextOptions<ReportStateDbContext> options) : DbContext(options)
{
    public DbSet<ReportRun> Runs => Set<ReportRun>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ReportRun>(e =>
        {
            e.ToTable("runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.ReportDate).HasMaxLength(10).IsRequired();
            e.Property(x => x.Trigger).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => new { x.ReportDate, x.Status });
            e.HasIndex(x => x.FinishedAtUnixMs);
            e.Ignore(x => x.StartedAt);
            e.Ignore(x => x.FinishedAt);
        });
    }
}
