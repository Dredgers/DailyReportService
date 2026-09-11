-- ================================================================
-- Make Me Red — Supabase schema
-- Run this in: the SHARED Competitive Games Supabase project (the same
-- project Competitive Crosswords uses) → SQL Editor → New query → Run.
--
-- This project's `public.users` (mirrors auth.users, own-row RLS, the
-- handle_new_user() trigger that creates the row on first sign-in
-- across ANY app in the project) already exists — it was created by
-- Competitive Crosswords' own server/sql/schema.sql and is the shared
-- identity table for the whole Competitive Games universe. Make Me Red
-- does NOT recreate it here; it only references public.users(id) as a
-- foreign key. See DECISIONS.md "Deployment: shared Supabase project".
-- ================================================================

-- ── Results ──────────────────────────────────────────────────────
-- NOTE: add_mode_to_mmr_results.sql (step 5 in README.md) widens this
-- to one row per (user, date, mode) — this file keeps its original
-- shape because structural changes go in new migration files, never
-- edits here. A fresh project runs this, then the migrations, in order.
--
-- One row per (user, calendar date) — mirrors the client's DayLog/DayEntry
-- shape exactly (started/moves/par/timeMs), plus a `verified` flag:
--
--   verified = true  — the server itself computed and validated this
--                       result (POST /api/result, after recomputing par
--                       from the date and rejecting implausible times —
--                       see server/app.ts). Leaderboard-eligible.
--   verified = false — folded in from a player's local guest history
--                       (POST /api/merge). An unverifiable client claim:
--                       kept for the player's own stats, but never
--                       counted on the daily leaderboard.
--
-- Named mmr_results (not results) because this table lives in the same
-- shared `public` schema as Competitive Crosswords' own tables — every
-- app in the universe prefixes its own tables to avoid collisions.
--
-- Only the server (service-role key) ever writes this table — see
-- grants.sql.
create table if not exists public.mmr_results (
  user_id      uuid not null references public.users(id) on delete cascade,
  date         text not null,               -- UTC calendar date, YYYY-MM-DD
  started      boolean not null default true,
  moves        integer,
  par          integer,
  time_ms      integer,
  verified     boolean not null default false,
  completed_at timestamptz,
  primary key (user_id, date)
);

alter table public.mmr_results enable row level security;

create policy "mmr_results: own rows read"
  on public.mmr_results for select
  using (auth.uid() = user_id);

-- No insert/update/delete policy for `authenticated` — see
-- server/sql/README.md for why writes are server-only.

create index if not exists mmr_results_date_verified_idx
  on public.mmr_results (date, verified, time_ms);
