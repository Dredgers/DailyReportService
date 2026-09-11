using DailyReport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DailyReport.IntegrationTests;

/// <summary>
/// Seeds rows the way production gets them: users arrive through auth.users and the trigger, everything else is
/// a plain insert as the admin (production's service role). Tests read back through <see cref="ReportRo"/>.
/// </summary>
public sealed class TestData : IAsyncDisposable
{
    private readonly NpgsqlDataSource admin = NpgsqlDataSource.Create(DatabaseFixture.AdminConnectionString);

    public static GamesDbContext ReportRo() => Create(DatabaseFixture.ReportRoConnectionString);

    public static GamesDbContext Admin() => Create(DatabaseFixture.AdminConnectionString);

    /// <summary>Empties every game table so each test starts from nothing. Sequences restart too.</summary>
    public async Task ResetAsync()
    {
        await ExecuteAsync(
            """
            truncate public.game_results, public.puzzles_attempted, public.events, public.ratings,
                     public.rating_events, public.rated_attempts, public.ghosts, public.named_ghosts,
                     public.mmr_results, public.mmr_profiles, public.puzzles, public.users, auth.users
            restart identity cascade
            """);
    }

    public async Task<Guid> CreateUserAsync(string? email = null, bool ghostConsent = false, DateTimeOffset? createdAt = null)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "insert into auth.users (id, email) values ($1, $2)",
            id, email ?? $"{id:N}@example.invalid");

        if (ghostConsent || createdAt is not null)
        {
            await ExecuteAsync(
                "update public.users set ghost_consent = $2, created_at = coalesce($3, created_at) where id = $1",
                id, ghostConsent, createdAt);
        }

        return id;
    }

    public async Task<string> CreatePuzzleAsync(string? id = null)
    {
        id ??= Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        await ExecuteAsync("insert into public.puzzles (id, title) values ($1, $2)", id, "Puzzle " + id[..6]);
        return id;
    }

    public async Task<long> AddGameResultAsync(
        Guid userId, string puzzleId, string mode, DateTimeOffset completedAt,
        int? durationS = 300, int score = 100, Guid? gameId = null, short? playerCount = 1,
        bool ranked = true, short ghostOpponents = 0)
    {
        return await ScalarAsync<long>(
            """
            insert into public.game_results
              (user_id, puzzle_id, mode, score, player_count, duration_s, completed_at, game_id, ranked, ghost_opponents)
            values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
            returning id
            """,
            userId, puzzleId, mode, score, playerCount, durationS, completedAt, gameId ?? Guid.NewGuid(), ranked, ghostOpponents);
    }

    public Task AddEventAsync(string name, DateTimeOffset createdAt, string? mode = null, short? playerCount = null, string device = "desktop", Guid? sessionId = null) =>
        ExecuteAsync(
            "insert into public.events (name, mode, device, player_count, session_id, created_at) values ($1, $2, $3, $4, $5, $6)",
            name, mode, device, playerCount, sessionId ?? Guid.NewGuid(), createdAt);

    public Task AddMmrResultAsync(Guid userId, DateOnly date, string mode = "three", bool started = true, DateTimeOffset? completedAt = null, int? moves = 12, int? par = 10, int? timeMs = 90_000, bool verified = true, int? attempts = 1) =>
        ExecuteAsync(
            """
            insert into public.mmr_results (user_id, date, mode, started, moves, par, time_ms, verified, completed_at, attempts)
            values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
            """,
            userId, date.ToString("yyyy-MM-dd"), mode, started, moves, par, timeMs, verified, completedAt, attempts);

    public async Task ExecuteAsync(string sql, params object?[] parameters)
    {
        await using var command = admin.CreateCommand(sql);
        foreach (var p in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = p ?? DBNull.Value });
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql, params object?[] parameters)
    {
        await using var command = admin.CreateCommand(sql);
        foreach (var p in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = p ?? DBNull.Value });
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    public ValueTask DisposeAsync() => admin.DisposeAsync();

    private static GamesDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GamesDbContext>().UseNpgsql(connectionString).Options;
        return new GamesDbContext(options);
    }
}
