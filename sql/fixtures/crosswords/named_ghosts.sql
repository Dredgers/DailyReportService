-- ================================================================
-- Competitive Crosswords — consented NAMED ghosts (opt-in second lane)
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after ghosts.sql / ghost_opponents.sql). See docs/MAKING_GHOSTS.md
-- "Consented named ghosts".
--
-- This is the OPPOSITE of the anonymous `ghosts` table. Signed-in players can opt in
-- (users.ghost_consent) to having their Solo solves replayed to others UNDER THEIR
-- NAME. Consent is the lawful basis, so attribution is intended — and therefore this
-- data is intentionally identifiable and fully under the user's control:
--   • user_id    — FK to the owner, ON DELETE CASCADE → account deletion erases it.
--   • display_name — a SNAPSHOT taken at capture (refreshed on the next solve), so a
--                    later rename doesn't silently relabel old ghosts (decision D9).
--   • one row per (user_id, puzzle_id) — re-solving upserts (refreshes the snapshot).
-- The anonymous `ghosts` table and its strict self-test are untouched by this.
-- ================================================================

-- Opt-in consent flag on the user (default OFF — opt-in, never opt-out).
alter table public.users
  add column if not exists ghost_consent boolean not null default false;

create table if not exists public.named_ghosts (
  ghost_id        uuid primary key default gen_random_uuid(),
  user_id         uuid not null references public.users(id) on delete cascade,
  puzzle_id       text not null references public.puzzles(id),
  display_name    text not null,
  schema_version  smallint not null,
  clue_events     jsonb not null,
  unique (user_id, puzzle_id)              -- one (latest) named ghost per user per puzzle
);

-- Read path: fetch a puzzle's named-ghost pool.
create index if not exists named_ghosts_puzzle_idx on public.named_ghosts (puzzle_id);

-- RLS on. Clients never read/write named ghosts directly — the server (service-role,
-- which bypasses RLS) reads them to spawn opponents and writes them on a consented
-- solve. No client policies by design.
alter table public.named_ghosts enable row level security;

grant all on table public.named_ghosts to service_role;
-- NOTE: see fix_service_role_grants.sql — Supabase's default privileges grant
-- this table to anon/authenticated regardless, so "no client policies by design"
-- needs an explicit revoke to be true of grants as well as policies.
