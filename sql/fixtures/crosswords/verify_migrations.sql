-- ================================================================
-- Competitive Crosswords — is the database actually migrated?
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
--
-- READ-ONLY. Reads catalogue views only; changes nothing. Every row should say
-- PASS; a FAIL names the file in server/sql/ still to run. Covers the migrations
-- whose absence fails SILENTLY at runtime rather than loudly at startup — the
-- server never inspects its own schema (it only ever connects with the
-- service-role key; see server/db.js), so a missing grant surfaces as data that
-- quietly never gets written, not as an error anyone sees.
--
-- Add a check here whenever a new migration lands.
-- ================================================================

with checks(step, check_name, ok) as (
  -- 10. fix_client_write_grants.sql
  select '10 fix_client_write_grants', 'game_results: authenticated has NO insert',
         not has_table_privilege('authenticated', 'public.game_results', 'INSERT')
  union all
  select '10 fix_client_write_grants', 'puzzles_attempted: authenticated has NO insert',
         not has_table_privilege('authenticated', 'public.puzzles_attempted', 'INSERT')
  union all
  select '10 fix_client_write_grants', 'puzzles_attempted: authenticated has NO update',
         not has_table_privilege('authenticated', 'public.puzzles_attempted', 'UPDATE')
  union all
  select '10 fix_client_write_grants', 'puzzles_attempted: authenticated has NO delete',
         not has_table_privilege('authenticated', 'public.puzzles_attempted', 'DELETE')
  union all
  select '10 fix_client_write_grants', 'puzzles_attempted: authenticated KEEPS select',
         has_table_privilege('authenticated', 'public.puzzles_attempted', 'SELECT')

  -- 11. ghost_pace.sql  (the COLUMN only — ops/backfill-pace.mjs fills existing
  -- rows, and this cannot see whether that ran; check with
  --   select count(*), count(pace_spl) from public.ghosts;)
  union all
  select '11 ghost_pace', 'ghosts.pace_spl column exists', exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'ghosts' and column_name = 'pace_spl')
  union all
  select '11 ghost_pace', 'named_ghosts.pace_spl column exists', exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'named_ghosts' and column_name = 'pace_spl')

  -- 12. ratings.sql
  union all
  select '12 ratings', 'tables ratings / rating_events / rated_attempts exist', (
    select count(*) = 3 from information_schema.tables
    where table_schema = 'public'
      and table_name in ('ratings', 'rating_events', 'rated_attempts'))
  union all
  select '12 ratings', 'RLS on for all three rating tables', (
    select count(*) = 3 from pg_class c join pg_namespace n on n.oid = c.relnamespace
    where n.nspname = 'public' and c.relrowsecurity
      and c.relname in ('ratings', 'rating_events', 'rated_attempts'))
  union all
  -- The silent one: `grant all on TABLE` does not cover the bigserial's sequence.
  -- Without this the audit insert 403s and rating_events never writes (ratings.sql:82).
  select '12 ratings', 'rating_events_id_seq USAGE granted to service_role', coalesce((
    select has_sequence_privilege('service_role', c.oid, 'USAGE')
    from pg_class c where c.oid = to_regclass('public.rating_events_id_seq')), false)
  union all
  select '12 ratings', 'rating_events (game_id,user_id) unique index', exists (
    select 1 from pg_indexes
    where schemaname = 'public' and indexname = 'rating_events_game_user_uidx')
  union all
  select '12 ratings', 'named_ghosts rating_snapshot + rd_snapshot columns', (
    select count(*) = 2 from information_schema.columns
    where table_schema = 'public' and table_name = 'named_ghosts'
      and column_name in ('rating_snapshot', 'rd_snapshot'))

  -- 13. fix_service_role_grants.sql — the tables that CLAIM to be service-role
  -- only. Supabase's default privileges grant every new public table to the
  -- client roles automatically, so "no grants by design" needs an explicit
  -- REVOKE to actually be true.
  union all
  select '13 fix_service_role_grants', 'no anon/authenticated grants on service-role tables', not exists (
    select 1 from information_schema.role_table_grants
    where table_schema = 'public'
      and table_name in ('ratings', 'rating_events', 'rated_attempts',
                         'ghosts', 'named_ghosts', 'events')
      and grantee in ('anon', 'authenticated'))
  union all
  select '13 fix_service_role_grants', 'no anon/authenticated USAGE on rating_events_id_seq', coalesce((
    select not (has_sequence_privilege('anon', c.oid, 'USAGE')
             or has_sequence_privilege('authenticated', c.oid, 'USAGE'))
    from pg_class c where c.oid = to_regclass('public.rating_events_id_seq')), false)

  -- 14. ghost_assisted.sql
  union all
  select '14 ghost_assisted', 'ghosts.assisted + named_ghosts.assisted columns', (
    select count(*) = 2 from information_schema.columns
    where table_schema = 'public' and column_name = 'assisted'
      and table_name in ('ghosts', 'named_ghosts'))
)
select case when ok then 'PASS' else 'FAIL  <-- run this file' end as result,
       step, check_name
from checks
order by ok, step, check_name;
