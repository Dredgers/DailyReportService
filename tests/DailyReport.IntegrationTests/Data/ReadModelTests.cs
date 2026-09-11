using DailyReport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DailyReport.IntegrationTests.Data;

public sealed class ReadModelTests
{
    private TestData data = null!;

    [SetUp]
    public async Task SetUp()
    {
        data = new TestData();
        await data.ResetAsync();
    }

    [TearDown]
    public async Task TearDown() => await data.DisposeAsync();

    [Test]
    public async Task Every_mapped_column_exists_in_the_real_schema()
    {
        // Selecting through each DbSet compiles a SELECT of every mapped column; a wrong name is a server error.
        await using var ctx = TestData.ReportRo();

        Assert.Multiple(async () =>
        {
            Assert.That(await ctx.Users.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.Puzzles.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.GameResults.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.PuzzlesAttempted.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.Events.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.Ratings.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.RatingEvents.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.RatedAttempts.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.Ghosts.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.NamedGhosts.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.MmrResults.Take(1).ToListAsync(), Is.Empty);
            Assert.That(await ctx.MmrProfiles.Take(1).ToListAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task Report_ro_reads_one_row_from_every_table_through_rls()
    {
        var user = await data.CreateUserAsync(ghostConsent: true);
        var puzzle = await data.CreatePuzzleAsync();
        var gameId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

        await data.AddGameResultAsync(user, puzzle, "solo", at, gameId: gameId);
        await data.ExecuteAsync("select public.record_attempt($1, $2, 'solo', 100)", user, puzzle);
        await data.AddEventAsync("game_finished", at, mode: "solo", playerCount: 1);
        await data.ExecuteAsync("insert into public.ratings (user_id) values ($1)", user);
        await data.ExecuteAsync(
            """
            insert into public.rating_events (game_id, user_id, puzzle_id, algo_version, rating_before, rd_before, rating_after, rd_after, outcome, opponents)
            values ($1, $2, $3, 1, 1500, 350, 1510, 300, 'completed', '[]'::jsonb)
            """, gameId, user, puzzle);
        await data.ExecuteAsync("insert into public.rated_attempts (user_id, puzzle_id, game_id) values ($1, $2, $3)", user, puzzle, gameId);
        await data.ExecuteAsync("insert into public.ghosts (puzzle_id, schema_version, clue_events) values ($1, 1, '[]'::jsonb)", puzzle);
        await data.ExecuteAsync("insert into public.named_ghosts (user_id, puzzle_id, display_name, schema_version, clue_events) values ($1, $2, 'T', 1, '[]'::jsonb)", user, puzzle);
        await data.AddMmrResultAsync(user, new DateOnly(2026, 9, 10), completedAt: at);
        await data.ExecuteAsync("insert into public.mmr_profiles (user_id, display_name) values ($1, 'T')", user);

        await using var ctx = TestData.ReportRo();

        Assert.Multiple(async () =>
        {
            Assert.That(await ctx.Users.CountAsync(), Is.EqualTo(1), "users");
            Assert.That(await ctx.Puzzles.CountAsync(), Is.EqualTo(1), "puzzles");
            Assert.That(await ctx.GameResults.CountAsync(), Is.EqualTo(1), "game_results");
            Assert.That(await ctx.PuzzlesAttempted.CountAsync(), Is.EqualTo(1), "puzzles_attempted");
            Assert.That(await ctx.Events.CountAsync(), Is.EqualTo(1), "events");
            Assert.That(await ctx.Ratings.CountAsync(), Is.EqualTo(1), "ratings");
            Assert.That(await ctx.RatingEvents.CountAsync(), Is.EqualTo(1), "rating_events");
            Assert.That(await ctx.RatedAttempts.CountAsync(), Is.EqualTo(1), "rated_attempts");
            Assert.That(await ctx.Ghosts.CountAsync(), Is.EqualTo(1), "ghosts");
            Assert.That(await ctx.NamedGhosts.CountAsync(), Is.EqualTo(1), "named_ghosts");
            Assert.That(await ctx.MmrResults.CountAsync(), Is.EqualTo(1), "mmr_results");
            Assert.That(await ctx.MmrProfiles.CountAsync(), Is.EqualTo(1), "mmr_profiles");
        });
    }

    [Test]
    public async Task Values_round_trip_with_the_types_the_report_relies_on()
    {
        var user = await data.CreateUserAsync(ghostConsent: true);
        var puzzle = await data.CreatePuzzleAsync();
        var at = new DateTimeOffset(2026, 9, 10, 21, 59, 59, TimeSpan.Zero);
        await data.AddGameResultAsync(user, puzzle, "competitive", at, durationS: null, playerCount: 3, ranked: false, ghostOpponents: 2);
        await data.AddMmrResultAsync(user, new DateOnly(2026, 9, 10), mode: "five", completedAt: at, attempts: null);

        await using var ctx = TestData.ReportRo();
        var result = await ctx.GameResults.SingleAsync();
        var mmr = await ctx.MmrResults.SingleAsync();
        var u = await ctx.Users.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.CompletedAt, Is.EqualTo(at));
            Assert.That(result.CompletedAt!.Value.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(result.DurationS, Is.Null, "a forfeit has no duration");
            Assert.That(result.PlayerCount, Is.EqualTo((short)3));
            Assert.That(result.Ranked, Is.False);
            Assert.That(result.GhostOpponents, Is.EqualTo((short)2));
            Assert.That(mmr.Date, Is.EqualTo("2026-09-10"));
            Assert.That(mmr.Mode, Is.EqualTo("five"));
            Assert.That(mmr.Attempts, Is.Null);
            Assert.That(u.GhostConsent, Is.True);
        });
    }

    [Test]
    public async Task Report_ro_cannot_write()
    {
        await using var connection = new NpgsqlConnection(DatabaseFixture.ReportRoConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("insert into public.events (name) values ('forged')", connection);

        var ex = Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.That(ex!.SqlState, Is.EqualTo(PostgresErrorCodes.InsufficientPrivilege));
    }

    [Test]
    public void The_context_refuses_to_save_even_as_admin()
    {
        using var ctx = TestData.Admin();
        ctx.Add(new Event { Name = "forged" });

        Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Test]
    public void Order_files_only_name_files_that_exist()
    {
        foreach (var game in new[] { "crosswords", "makemered" })
        {
            var dir = Path.Combine(SqlScripts.SqlRoot, "fixtures", game);
            foreach (var file in SqlScripts.Order(dir))
            {
                Assert.That(File.Exists(Path.Combine(dir, file)), Is.True, $"{game}/{file}");
            }
        }
    }
}
