-- ================================================================
-- Make Me Red — one-time migration: mir_results -> mmr_results
--
-- Run this ONCE, in the shared Competitive Games Supabase project's SQL
-- Editor, if you already ran the old schema.sql/grants.sql back when
-- this table was still named mir_results (from the app's previous name,
-- "Make It Red") — before the app was renamed to Make Me Red.
--
-- A brand-new project that never had mir_results does NOT need this:
-- schema.sql already creates mmr_results directly.
-- ================================================================

alter table if exists public.mir_results rename to mmr_results;

alter index if exists mir_results_date_verified_idx
  rename to mmr_results_date_verified_idx;

-- Renaming a table does not rename its policies, so this stays
-- "mir_results: own rows read" unless renamed explicitly. Skip this line
-- if you already renamed it (ALTER POLICY doesn't support IF EXISTS).
alter policy "mir_results: own rows read" on public.mmr_results
  rename to "mmr_results: own rows read";

-- Table-level grants (service_role/authenticated) and the foreign key to
-- public.users survive a rename automatically (Postgres tracks them by
-- OID, not name) — no need to re-run grants.sql. Ask PostgREST to pick
-- up the new name immediately rather than waiting for its own schema
-- cache to refresh:
notify pgrst, 'reload schema';
