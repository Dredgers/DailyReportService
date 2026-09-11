-- ================================================================
-- Competitive Crosswords — "human topped the ghosts" flag on game results
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after ghost_opponents.sql).
--
--   human_won_vs_ghosts — for a Competitive race that INCLUDED ghosts, did the
--     leading human score at least as high as every ghost? Recorded on every row
--     of the game (the value is a property of the whole race). The competitive
--     "best time today" board lists one row per race; a race that involved ghosts
--     appears ONLY when this is true — i.e. a human actually beat the ghosts, so
--     the headline score is a real human achievement rather than a ghost's.
--
--     NULL means the rule doesn't apply: solo games, and Competitive games with no
--     ghosts (those always qualify). FALSE means ghosts were present and out-scored
--     the humans (the race is hidden from the time board). Older rows read as NULL
--     and so are treated as "no ghost gate" — correct, since the flag is only ever
--     consulted for games whose ghost_opponents > 0.
--
-- Backward-compatible: nullable add, safe to run on a live table.
-- ================================================================

alter table public.game_results
  add column if not exists human_won_vs_ghosts boolean;
