-- ================================================================
-- Competitive Crosswords — leaderboard support
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after schema.sql / grants.sql / record_attempt.sql).
--
-- Adds two columns to game_results that the speed leaderboards need:
--
--   game_id   — one shared id for all the rows written by a single finished
--               game. A multi-player game writes one row per signed-in player;
--               grouping by game_id collapses those rows into ONE board entry
--               where a board needs a per-game (not per-player) view.
--
--   is_public — whether the game was a Quick Play (public matchmaking) room.
--               Speed boards ignore it, but the future ranked competitive points
--               board will count ONLY public games, so you can't farm points in a
--               private room against friends/alts. Captured now so the data exists
--               when that board ships. Defaults false (older rows = not ranked).
--
-- Both are nullable / defaulted, so this is a safe, backward-compatible migration.
-- ================================================================

alter table public.game_results add column if not exists game_id   uuid;
alter table public.game_results add column if not exists is_public  boolean not null default false;

-- Speed-board read path: filter by mode + puzzle, order by solve time, often with
-- a recent-window (completed_at) filter. This composite covers it.
create index if not exists game_results_speed_idx
  on public.game_results (mode, puzzle_id, duration_s)
  where duration_s is not null;

-- The "this week" recent-solvers filter scans by completed_at.
create index if not exists game_results_completed_idx
  on public.game_results (completed_at);

-- Grouping a multi-player game's rows by their shared game id.
create index if not exists game_results_game_id_idx
  on public.game_results (game_id);
