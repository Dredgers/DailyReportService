-- ================================================================
-- Make Me Red — per-app profile (username + synced settings).
-- Run this in: the SHARED Competitive Games Supabase project (the same
-- one Competitive Crosswords uses) → SQL Editor → New query → Run.
--
-- public.users already exists (the shared identity table created by
-- Competitive Crosswords' schema). This only references it. See
-- server/sql/schema.sql and DECISIONS.md "Deployment: shared Supabase".
-- ================================================================

-- One row per user. `display_name` is the player's Make Me Red name,
-- independent of the shared public.users.display_name (so a player can be
-- called something different here than in Competitive Crosswords — the open
-- item from todo.md). `settings` is the client's settings blob (symbols,
-- guidingLights, …) synced so preferences follow the player across devices.
--
-- Named mmr_profiles (not profiles) because it lives in the same shared
-- `public` schema as Competitive Crosswords' tables — every app prefixes.
-- Only the server (service-role key) ever writes it — see grants.sql.
create table if not exists public.mmr_profiles (
  user_id      uuid primary key references public.users(id) on delete cascade,
  display_name text,
  settings     jsonb not null default '{}'::jsonb,
  updated_at   timestamptz not null default now()
);

alter table public.mmr_profiles enable row level security;

-- Idempotent: re-running this file must be safe, since the only way to
-- discover it was never run is a 500 in production (which is exactly how
-- it WAS discovered, 2026-09-02).
drop policy if exists "mmr_profiles: own row read" on public.mmr_profiles;
create policy "mmr_profiles: own row read"
  on public.mmr_profiles for select
  using (auth.uid() = user_id);

-- No insert/update/delete policy for `authenticated` — writes are
-- server-only (service-role key), same as mmr_results.

-- Grants, mirroring grants.sql for mmr_results. RLS decides WHICH rows a
-- role may see; grants decide whether it may touch the table at all —
-- without these the server's service-role key gets "permission denied"
-- even though the table now exists. Omitting them was the second half of
-- the 2026-09-02 outage.
grant usage  on schema public       to service_role;
grant all    on table  public.mmr_profiles to service_role;
grant usage  on schema public       to authenticated;
grant select on table  public.mmr_profiles to authenticated;
