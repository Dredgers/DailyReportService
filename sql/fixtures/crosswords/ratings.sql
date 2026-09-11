-- ================================================================
-- Competitive Crosswords — Glicko-1 ratings core
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after schema.sql / grants.sql / named_ghosts.sql).
-- Ticket: docs/dev_docs/GHOST_RATINGS_JOBS.md Phase 3a
-- Design: docs/Ideas/GHOST_TRANSPLANTS_AND_RATINGS.md §8
--
-- Three tables + two snapshot columns, ALL service-role only (the ghosts-table
-- pattern: RLS on, NO policies, NO authenticated/anon grants — the last of which
-- needs the explicit revoke in fix_service_role_grants.sql, because Supabase's
-- default privileges grant new public tables to those roles automatically). Only the rating
-- engine (server, service-role key) ever writes ratings; clients read their own
-- rating via the server (/api/me, end-of-game message), never the table. A rating
-- column on `users` was rejected: authenticated already holds table-wide UPDATE on
-- its own users row (grants.sql:18), which would make a naive rating client-editable.
-- ================================================================

-- ── ratings: the live per-user rating (one row per rated user) ──────
--   rating/rd     Glicko-1 skill estimate + its uncertainty (see server/rating.js).
--   races         count of rated races (leaderboard eligibility: races ≥ 5 ∧ rd ≤ 200).
--   last_rated_at drives idle RD inflation before the next race.
--   updated_at    optimistic-concurrency guard for the two-tabs write race
--                 (UPDATE … WHERE updated_at = seen).
create table if not exists public.ratings (
  user_id        uuid primary key references public.users(id) on delete cascade,
  rating         real not null default 1500,
  rd             real not null default 350,
  races          integer not null default 0,
  last_rated_at  timestamptz,
  updated_at     timestamptz not null default now()
);

alter table public.ratings enable row level security;   -- no policies: clients never touch it
grant all on table public.ratings to service_role;      -- server only

-- ── rating_events: the audit log (the diligence artifact) ───────────
-- Replaying these in id order under algo_version reproduces `ratings` exactly —
-- that is the artifact a newspaper-licensing review is shown. NUMBERS ONLY:
--   opponents = [{kind:'human'|'named_ghost'|'anon_ghost'|'transplant', rating, rd, s}]
--   — no opponent user_ids (no deletion-cascade entanglement in an audit log) and no
--   ghost_ids (keeps the log clean under the anonymization self-test's "all
--   application logs" clause). This row's own user_id is a plain uuid (NOT a foreign
--   key): the audit persists even after the account is deleted, and stays replayable.
create table if not exists public.rating_events (
  id             bigserial primary key,
  game_id        uuid,
  user_id        uuid not null,
  puzzle_id      text,
  algo_version   smallint not null,
  rating_before  real not null,
  rd_before      real not null,
  rating_after   real not null,
  rd_after       real not null,
  outcome        text not null check (outcome in ('completed', 'abandoned')),
  opponents      jsonb not null,
  created_at     timestamptz not null default now()
);

-- Per-user replay in event order (id is globally ordered; this scopes it per user).
create index if not exists rating_events_user_idx on public.rating_events (user_id, id);

-- At most one rated event per (game, user): a DB backstop against a double-seated
-- account or a re-entrant apply writing two updates for one game (the server also
-- guards this in-process). game_id is always set for a rated event.
create unique index if not exists rating_events_game_user_uidx
  on public.rating_events (game_id, user_id);

alter table public.rating_events enable row level security;
grant all on table public.rating_events to service_role;
-- The bigserial `id` draws from a sequence, and `grant all on TABLE` does NOT cover it.
-- Without this the service-role insert 403s ("permission denied for sequence
-- rating_events_id_seq") and the audit log silently never writes — verified against the
-- real DB. Matches grants.sql / events_grants.sql for the other bigserial tables.
grant usage, select on sequence public.rating_events_id_seq to service_role;

-- ── rated_attempts: the one-rated-attempt lock, taken at race START ─
-- Inserted when a rated race starts, so a mid-race leaver can't retry "first"
-- attempts indefinitely (recordGameResults only fires at completion/forfeit for
-- present seats). Predicate for a fresh rated attempt: no existing row for
-- (user, puzzle) with a DIFFERENT game_id. Abandon-before-underway deletes the
-- row (no burned attempt); abandon-after keeps it (a rated loss).
create table if not exists public.rated_attempts (
  user_id     uuid not null references public.users(id) on delete cascade,
  puzzle_id   text not null references public.puzzles(id),
  game_id     uuid not null,
  started_at  timestamptz not null default now(),
  primary key (user_id, puzzle_id)
);

alter table public.rated_attempts enable row level security;
grant all on table public.rated_attempts to service_role;

-- ── named_ghosts: frozen rating snapshot (named lane only) ──────────
-- Stamped at capture from the recorder's current rating; null for pre-rating
-- recordings; frozen forever (the ghost is a fossil). Racing one is a normal
-- pairwise match vs the snapshot with opponent RD = max(rd_snapshot, 100).
-- Anonymous ghosts never carry snapshots (guest-only by construction) — bands are
-- their only label. Both tables stay service-role only, so no new grants needed.
alter table public.named_ghosts
  add column if not exists rating_snapshot real,
  add column if not exists rd_snapshot     real;

-- ================================================================
-- Verification — run after applying the above.
-- ================================================================

-- 1. RLS is ON for all three new tables. Expect rowsecurity = true for each.
select relname, relrowsecurity as rls_on
from pg_class
where relname in ('ratings', 'rating_events', 'rated_attempts')
order by relname;

-- 2. Grants: ONLY service_role holds privileges on the three tables (no
--    authenticated/anon rows expected).
select table_name, grantee, privilege_type
from information_schema.role_table_grants
where table_schema = 'public'
  and table_name in ('ratings', 'rating_events', 'rated_attempts')
order by table_name, grantee, privilege_type;

-- 3. The named_ghosts snapshot columns exist.
select column_name, data_type
from information_schema.columns
where table_schema = 'public' and table_name = 'named_ghosts'
  and column_name in ('rating_snapshot', 'rd_snapshot')
order by column_name;
