-- ================================================================
-- Competitive Crosswords — ghost-opponent count on game results
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after leaderboard.sql).
--
--   ghost_opponents — how many synthetic "ghost" opponents shared a Competitive
--     race with the player (see docs/MAKING_GHOSTS.md). Recorded ONLY on the human's
--     own row, so their personal history/stats are honest ("you + 1 ghost") from day
--     one. It is NEVER used by the public points board: that board counts human
--     opponents only (player_count − placement), and player_count already excludes
--     ghosts by construction (ghosts are seatless — not in room.players). So a ghost
--     can never inflate a ranked board; this column is purely for the player's own
--     record. Defaults 0, so older rows and non-ghost games read correctly.
--
-- Backward-compatible: nullable-with-default add, safe to run on a live table.
-- ================================================================

alter table public.game_results
  add column if not exists ghost_opponents smallint not null default 0;
