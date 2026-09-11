-- ================================================================
-- Competitive Crosswords — grants for the events table + a read-only
-- dashboard role (Grafana). Run AFTER events.sql, in the SQL Editor.
-- ================================================================

-- service_role: the server inserts events (and runs the purge).
grant usage  on schema public to service_role;
grant all    on table  public.events to service_role;
grant usage, select on sequence public.events_id_seq to service_role;

-- anon / authenticated: NOTHING. Events are never read by the browser.
-- No grant lines on purpose — but absence is NOT enough on Supabase: the project
-- bootstrap's default privileges grant every new public table to both roles. RLS
-- (events.sql:28) is what actually denies them; fix_service_role_grants.sql adds
-- the matching revoke. Same trap the cc_dashboard_ro default-privilege revoke
-- below already sidesteps.

-- ── Read-only dashboard role (Grafana / Metabase / psql) ─────────
-- A login role that can SELECT the analytics tables and nothing else. Give it a
-- strong password and use it as the Grafana Postgres data source. It cannot
-- write, cannot see users/auth, cannot reach anything not granted below.
--
-- SECURITY: this file is tracked in git — do NOT commit a real password here.
-- Paste the real value only into the SQL editor when you run it, and store it in
-- ops/grafana/.env (gitignored) as CC_DB_PASSWORD. The `if not exists` guard
-- means this line is inert once the role exists, so the placeholder can stay.
do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'cc_dashboard_ro') then
    create role cc_dashboard_ro login password 'CHANGE_ME_STRONG_PASSWORD';
  end if;
end $$;

grant usage  on schema public to cc_dashboard_ro;
-- Only the non-personal analytics surfaces. game_results is per-user but has no
-- names/emails; include it so dashboards can chart scores/durations. Do NOT grant
-- users / puzzles_attempted / ghosts here — those are personal.
grant select on table public.events       to cc_dashboard_ro;
grant select on table public.game_results to cc_dashboard_ro;
grant select on table public.puzzles      to cc_dashboard_ro;

-- Lock it out of anything added later by default (defence in depth).
alter default privileges in schema public revoke all on tables from cc_dashboard_ro;
