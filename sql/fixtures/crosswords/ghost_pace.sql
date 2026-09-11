-- ================================================================
-- Competitive Crosswords — pace_spl on both ghost tables
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after ghosts.sql / named_ghosts.sql). Backfill existing rows with
-- ops/backfill-pace.mjs after applying this.
--
-- pace_spl (seconds per letter, cruise pace = median per-letter gap — see
-- docs/Ideas/GHOST_TRANSPLANTS_AND_RATINGS.md §3) is a deliberate, argued
-- exception to MAKING_GHOSTS' "no other columns" rule: it is a pure function
-- of clue_events already stored in the row, so it adds zero bits an attacker
-- with DB access couldn't already derive. Same exception class as
-- schema_version. See docs/dev_docs/MAKING_GHOSTS.md.
-- ================================================================

alter table public.ghosts
  add column if not exists pace_spl real;

alter table public.named_ghosts
  add column if not exists pace_spl real;

-- No new grants/policies needed: both tables remain service-role only.
