using Microsoft.EntityFrameworkCore;

namespace DailyReport.Infrastructure.Data;

/// <summary>
/// Read-only view of the shared games database. Every query is no-tracking and every write path throws:
/// the service connects as <c>report_ro</c>, which has SELECT only, and this context makes the same promise in code.
/// </summary>
public sealed class GamesDbContext(DbContextOptions<GamesDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Puzzle> Puzzles => Set<Puzzle>();

    public DbSet<GameResult> GameResults => Set<GameResult>();

    public DbSet<PuzzleAttempted> PuzzlesAttempted => Set<PuzzleAttempted>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<RatingEvent> RatingEvents => Set<RatingEvent>();

    public DbSet<RatedAttempt> RatedAttempts => Set<RatedAttempt>();

    public DbSet<Ghost> Ghosts => Set<Ghost>();

    public DbSet<NamedGhost> NamedGhosts => Set<NamedGhost>();

    public DbSet<MmrResult> MmrResults => Set<MmrResult>();

    public DbSet<MmrProfile> MmrProfiles => Set<MmrProfile>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.DisplayName).HasColumnName("display_name");
            e.Property(x => x.ColourKey).HasColumnName("colour_key");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.GhostConsent).HasColumnName("ghost_consent");
        });

        b.Entity<Puzzle>(e =>
        {
            e.ToTable("puzzles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.SourceFile).HasColumnName("source_file");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<GameResult>(e =>
        {
            e.ToTable("game_results");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.PuzzleId).HasColumnName("puzzle_id");
            e.Property(x => x.Mode).HasColumnName("mode");
            e.Property(x => x.Score).HasColumnName("score");
            e.Property(x => x.Placement).HasColumnName("placement");
            e.Property(x => x.PlayerCount).HasColumnName("player_count");
            e.Property(x => x.DurationS).HasColumnName("duration_s");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.GameId).HasColumnName("game_id");
            e.Property(x => x.IsPublic).HasColumnName("is_public");
            e.Property(x => x.GhostOpponents).HasColumnName("ghost_opponents");
            e.Property(x => x.Ranked).HasColumnName("ranked");
            e.Property(x => x.HumanWonVsGhosts).HasColumnName("human_won_vs_ghosts");
        });

        b.Entity<PuzzleAttempted>(e =>
        {
            e.ToTable("puzzles_attempted");
            e.HasKey(x => new { x.UserId, x.PuzzleId, x.Mode });
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.PuzzleId).HasColumnName("puzzle_id");
            e.Property(x => x.Mode).HasColumnName("mode");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.BestScore).HasColumnName("best_score");
        });

        b.Entity<Event>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Mode).HasColumnName("mode");
            e.Property(x => x.Device).HasColumnName("device");
            e.Property(x => x.PlayerCount).HasColumnName("player_count");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<Rating>(e =>
        {
            e.ToTable("ratings");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Value).HasColumnName("rating");
            e.Property(x => x.Rd).HasColumnName("rd");
            e.Property(x => x.Races).HasColumnName("races");
            e.Property(x => x.LastRatedAt).HasColumnName("last_rated_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<RatingEvent>(e =>
        {
            e.ToTable("rating_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.GameId).HasColumnName("game_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.PuzzleId).HasColumnName("puzzle_id");
            e.Property(x => x.AlgoVersion).HasColumnName("algo_version");
            e.Property(x => x.RatingBefore).HasColumnName("rating_before");
            e.Property(x => x.RdBefore).HasColumnName("rd_before");
            e.Property(x => x.RatingAfter).HasColumnName("rating_after");
            e.Property(x => x.RdAfter).HasColumnName("rd_after");
            e.Property(x => x.Outcome).HasColumnName("outcome");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<RatedAttempt>(e =>
        {
            e.ToTable("rated_attempts");
            e.HasKey(x => new { x.UserId, x.PuzzleId });
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.PuzzleId).HasColumnName("puzzle_id");
            e.Property(x => x.GameId).HasColumnName("game_id");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
        });

        b.Entity<Ghost>(e =>
        {
            e.ToTable("ghosts");
            e.HasKey(x => x.GhostId);
            e.Property(x => x.GhostId).HasColumnName("ghost_id");
            e.Property(x => x.PuzzleId).HasColumnName("puzzle_id");
            e.Property(x => x.SchemaVersion).HasColumnName("schema_version");
            e.Property(x => x.PaceSpl).HasColumnName("pace_spl");
            e.Property(x => x.Assisted).HasColumnName("assisted");
        });

        b.Entity<NamedGhost>(e =>
        {
            e.ToTable("named_ghosts");
            e.HasKey(x => x.GhostId);
            e.Property(x => x.GhostId).HasColumnName("ghost_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.PuzzleId).HasColumnName("puzzle_id");
            e.Property(x => x.DisplayName).HasColumnName("display_name");
            e.Property(x => x.SchemaVersion).HasColumnName("schema_version");
            e.Property(x => x.PaceSpl).HasColumnName("pace_spl");
            e.Property(x => x.RatingSnapshot).HasColumnName("rating_snapshot");
            e.Property(x => x.RdSnapshot).HasColumnName("rd_snapshot");
            e.Property(x => x.Assisted).HasColumnName("assisted");
        });

        b.Entity<MmrResult>(e =>
        {
            e.ToTable("mmr_results");
            e.HasKey(x => new { x.UserId, x.Date, x.Mode });
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Date).HasColumnName("date");
            e.Property(x => x.Mode).HasColumnName("mode");
            e.Property(x => x.Started).HasColumnName("started");
            e.Property(x => x.Moves).HasColumnName("moves");
            e.Property(x => x.Par).HasColumnName("par");
            e.Property(x => x.TimeMs).HasColumnName("time_ms");
            e.Property(x => x.Verified).HasColumnName("verified");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.Attempts).HasColumnName("attempts");
        });

        b.Entity<MmrProfile>(e =>
        {
            e.ToTable("mmr_profiles");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.DisplayName).HasColumnName("display_name");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) => throw ReadOnly();

    private static InvalidOperationException ReadOnly() =>
        new("GamesDbContext is read-only: the Daily Report Service never writes to game tables.");
}
