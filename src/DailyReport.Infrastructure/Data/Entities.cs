namespace DailyReport.Infrastructure.Data;

// Read-only mirrors of the tables in the shared Supabase project. Column names and types follow the
// SQL files under sql/fixtures exactly; the integration fixture applies those files and selects every
// mapped column, so a drift here fails a test rather than a morning report.
// jsonb payloads (clue_events, opponents, settings) are deliberately not mapped: the report never reads them.

/// <summary>
/// public.users (crosswords schema.sql + named_ghosts.sql). One row per account, created by the auth trigger.
/// Only the columns the report needs are mapped and granted: email, display name and colour stay out of reach.
/// </summary>
public sealed class User
{
    public Guid Id { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Opt-in to the named-ghost lane. Default false; opting out deletes the player's named ghosts.</summary>
    public bool GhostConsent { get; init; }
}

/// <summary>public.puzzles in the shared project: a stub keyed by content hash. No status, no used_at; those live in the puzzle-source project.</summary>
public sealed class Puzzle
{
    public string Id { get; init; } = "";

    public string? Title { get; init; }

    public string? SourceFile { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>
/// public.game_results: one row per signed-in player per completed game. A give-up is a row with <see cref="DurationS"/> null.
/// <see cref="Ranked"/> is false for a replay of a grid the player already completed in any mode.
/// </summary>
public sealed class GameResult
{
    public long Id { get; init; }

    public Guid UserId { get; init; }

    public string PuzzleId { get; init; } = "";

    /// <summary>'competitive' | 'chaotic' | 'solo'.</summary>
    public string Mode { get; init; } = "";

    public int Score { get; init; }

    public short? Placement { get; init; }

    /// <summary>Humans in the room. Ghosts are seatless and never counted.</summary>
    public short? PlayerCount { get; init; }

    /// <summary>Seconds from first keypress to completion. Null means a forfeit ("Show all"), which is not a solve.</summary>
    public int? DurationS { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Shared by every row written by one finished game; groups a race's participants.</summary>
    public Guid? GameId { get; init; }

    public bool IsPublic { get; init; }

    public short GhostOpponents { get; init; }

    public bool Ranked { get; init; }

    public bool? HumanWonVsGhosts { get; init; }
}

/// <summary>public.puzzles_attempted. Written only by record_attempt() at completion, so <see cref="StartedAt"/> is not a start signal.</summary>
public sealed class PuzzleAttempted
{
    public Guid UserId { get; init; }

    public string PuzzleId { get; init; } = "";

    public string Mode { get; init; } = "";

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public int? BestScore { get; init; }
}

/// <summary>public.events: the crosswords funnel. No user id by design; <see cref="SessionId"/> is per page load.</summary>
public sealed class Event
{
    public long Id { get; init; }

    /// <summary>landing_view | room_created | invite_joined | game_started | game_finished.</summary>
    public string Name { get; init; } = "";

    public string? Mode { get; init; }

    /// <summary>'phone' | 'desktop'.</summary>
    public string? Device { get; init; }

    public short? PlayerCount { get; init; }

    public Guid? SessionId { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>public.ratings: Glicko-1 state per player.</summary>
public sealed class Rating
{
    public Guid UserId { get; init; }

    public float Value { get; init; }

    public float Rd { get; init; }

    public int Races { get; init; }

    public DateTimeOffset? LastRatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>public.rating_events: one row per player per rated race. <see cref="UserId"/> is a plain uuid, not a foreign key.</summary>
public sealed class RatingEvent
{
    public long Id { get; init; }

    public Guid? GameId { get; init; }

    public Guid UserId { get; init; }

    public string? PuzzleId { get; init; }

    public short AlgoVersion { get; init; }

    public float RatingBefore { get; init; }

    public float RdBefore { get; init; }

    public float RatingAfter { get; init; }

    public float RdAfter { get; init; }

    /// <summary>'completed' | 'abandoned'.</summary>
    public string Outcome { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>public.rated_attempts: the race-start lock. Deleted again on abandon-before-underway, so not a complete start log.</summary>
public sealed class RatedAttempt
{
    public Guid UserId { get; init; }

    public string PuzzleId { get; init; } = "";

    public Guid GameId { get; init; }

    public DateTimeOffset StartedAt { get; init; }
}

/// <summary>public.ghosts: the anonymous lane. No timestamp, no user link, by design. Count only.</summary>
public sealed class Ghost
{
    public Guid GhostId { get; init; }

    public string PuzzleId { get; init; } = "";

    public short SchemaVersion { get; init; }

    public float? PaceSpl { get; init; }

    public bool Assisted { get; init; }
}

/// <summary>public.named_ghosts: the consented lane. Also no timestamp. Count only.</summary>
public sealed class NamedGhost
{
    public Guid GhostId { get; init; }

    public Guid UserId { get; init; }

    public string PuzzleId { get; init; } = "";

    public short SchemaVersion { get; init; }

    public float? PaceSpl { get; init; }

    public float? RatingSnapshot { get; init; }

    public float? RdSnapshot { get; init; }

    public bool Assisted { get; init; }
}

/// <summary>
/// public.mmr_results: one row per signed-in player per UTC date per mode. <see cref="Date"/> is text 'YYYY-MM-DD'.
/// <see cref="CompletedAt"/> is set on a verified submit and on a merge that carried a move count.
/// </summary>
public sealed class MmrResult
{
    public Guid UserId { get; init; }

    public string Date { get; init; } = "";

    /// <summary>'three' | 'four' | 'five'.</summary>
    public string Mode { get; init; } = "";

    public bool Started { get; init; }

    public int? Moves { get; init; }

    public int? Par { get; init; }

    public int? TimeMs { get; init; }

    public bool Verified { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public int? Attempts { get; init; }
}

/// <summary>public.mmr_profiles: per-app display name and synced settings.</summary>
public sealed class MmrProfile
{
    public Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
