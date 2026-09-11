-- ================================================================
-- Make Me Red — PostgREST role grants
-- Run this in: the SHARED Competitive Games Supabase project → SQL
-- Editor → New query → Run. Only needed once.
--
-- No grants for public.users here — Competitive Crosswords' own
-- grants.sql already gave service_role full access and authenticated
-- select+update on that shared table. This file only wires up
-- mmr_results, the table Make Me Red owns.
-- ================================================================

-- service_role: full access (server-side writes)
grant usage on schema public to service_role;
grant all   on table  public.mmr_results to service_role;

-- authenticated: read own rows only (RLS enforces ownership).
-- Write access is intentionally NOT granted here — see
-- server/sql/README.md for why results must be server-only.
grant usage  on schema public to authenticated;
grant select on table  public.mmr_results to authenticated;
