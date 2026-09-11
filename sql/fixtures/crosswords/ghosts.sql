-- ================================================================
-- Competitive Crosswords — ghost recordings (anonymized)
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after schema.sql / grants.sql). EU-region project only (GDPR data
-- residency — see docs/MAKING_GHOSTS.md "Anonymization requirements").
--
-- A ghost is a synthetic opponent built from a real solo solve. This table is
-- deliberately MINIMAL and one-way anonymous: there is no path from a ghost back
-- to the user who generated it.
--   ghost_id        random UUID — not sequential, not time-ordered.
--   puzzle_id       which puzzle it plays.
--   schema_version  smallint format version (decision #3). NOT an identity vector —
--                   the "no other columns" rule targets re-identification, and a
--                   format version is not personal data — so it's allowed.
--   clue_events     [{clue_id, highlighted_offset_ms, completed_offset_ms}], where
--                   offsets are milliseconds from race start (never wall-clock).
-- Specifically NOT stored: any timestamp column, user_id/solve_id/source link,
-- IP/session/device, display name, or a seed flag that references an account.
-- ================================================================

create table if not exists public.ghosts (
  ghost_id        uuid primary key default gen_random_uuid(),
  puzzle_id       text not null references public.puzzles(id),
  schema_version  smallint not null,
  clue_events     jsonb not null
);

-- Read path: fetch a puzzle's ghost pool.
create index if not exists ghosts_puzzle_idx on public.ghosts (puzzle_id);

-- RLS on, with NO policies: clients (anon/authenticated) can never read or write
-- ghosts directly — they only ever experience them via server-emitted WS events.
-- The service-role key (server only) bypasses RLS.
alter table public.ghosts enable row level security;

-- Grants: server (service_role) only.
grant all on table public.ghosts to service_role;
-- NOTE: granting service_role is not sufficient to keep anon/authenticated out.
-- Supabase's default privileges grant every new public table to them anyway —
-- see fix_service_role_grants.sql, which revokes it. RLS above already denies
-- them all DML; the revoke closes TRUNCATE/REFERENCES/TRIGGER.
