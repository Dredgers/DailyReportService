-- ================================================================
-- Competitive Crosswords — close two client-writable integrity holes
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- Ticket: docs/dev_docs/GHOST_RATINGS_JOBS.md Phase 0a
-- Design: docs/Ideas/GHOST_TRANSPLANTS_AND_RATINGS.md §12
--
-- Two tables grant the `authenticated` PostgREST role write access that no
-- legitimate client path uses — the server always writes both with the
-- service-role key (server/db.js; recordGameResults / the record_attempt RPC).
-- Left as-is, a signed-in client can:
--   1. INSERT a forged row directly into game_results (arbitrary
--      score/placement/duration; ranked defaults true) — poisons leaderboards
--      today, and would poison rating calibration once ratings ship.
--   2. DELETE its own puzzles_attempted row, resetting isFirstCompletion and
--      farming "first ranked attempt" repeatedly on the same puzzle.
-- This script revokes both write paths and narrows the RLS policies to match.
-- Verified pre-fix: no server code path uses the `authenticated`-scoped client
-- to write either table — see verification queries at the bottom.
-- ================================================================

-- ── game_results: read-your-own stays, INSERT goes ──────────────
revoke insert on table public.game_results from authenticated;

drop policy if exists "game_results: own rows insert" on public.game_results;

-- (the "game_results: own rows read" select policy is untouched)

-- ── puzzles_attempted: read-your-own stays, write goes entirely ─
-- grants.sql:21 used `grant all`, which in Postgres also covers TRUNCATE,
-- REFERENCES and TRIGGER — not just insert/update/delete. Revoke ALL then
-- re-grant SELECT explicitly, so no forgotten privilege type is left behind.
revoke all on table public.puzzles_attempted from authenticated;
grant select on table public.puzzles_attempted to authenticated;

drop policy if exists "puzzles_attempted: own rows write" on public.puzzles_attempted;

-- (the "puzzles_attempted: own rows read" select policy is untouched)

-- ================================================================
-- Verification — run before AND after applying the revokes above.
-- ================================================================

-- 1. Grants on the two tables, per role. Expect BEFORE: authenticated has
--    INSERT on game_results and INSERT/UPDATE/DELETE on puzzles_attempted.
--    Expect AFTER: authenticated has SELECT only on both.
select grantee, table_name, privilege_type
from information_schema.role_table_grants
where table_schema = 'public'
  and table_name in ('game_results', 'puzzles_attempted')
  and grantee in ('authenticated', 'service_role')
order by table_name, grantee, privilege_type;

-- 2. Policies remaining on the two tables. Expect AFTER: exactly one policy
--    per table (the "own rows read" select policy) — the write policies gone.
select schemaname, tablename, policyname, cmd
from pg_policies
where tablename in ('game_results', 'puzzles_attempted')
order by tablename, policyname;
