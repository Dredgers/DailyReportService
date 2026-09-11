-- ================================================================
-- Competitive Crosswords — "one grid, one ranked life" (cross-mode)
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after schema.sql / leaderboard.sql / ghost_opponents.sql).
--
--   ranked — whether this completion counts toward the PUBLIC leaderboards.
--     TRUE only for a player's FIRST completion of a given puzzle ACROSS ALL MODES.
--     A replay — the same grid again, in any mode, where you already know the
--     answers — is still recorded (personal history stays honest) but ranked=false,
--     so it can NEVER reach a board. This is the rule "one grid, one ranked life":
--     a Solo solve spends the grid for ranking, so a later Competitive race on it
--     doesn't count, and vice-versa.
--
--     Decided ONCE at record time (server.js recordGameResults checks whether a
--     puzzles_attempted row already exists for this user+puzzle in any mode) and
--     frozen on the row. Every present and future board simply filters ranked=true,
--     so the rule is inherited for free and can't be bypassed by the client-supplied
--     matchmaking `attempted` list (which only steers auto-select, never scoring).
--
--     Defaults true, so older rows and every genuine first solve count.
--
-- Backward-compatible: nullable-with-default add, safe to run on a live table.
-- ================================================================

alter table public.game_results
  add column if not exists ranked boolean not null default true;

-- Boards always filter ranked=true; a partial index keeps those scans tight.
create index if not exists game_results_ranked_idx
  on public.game_results (mode, ranked)
  where ranked;
