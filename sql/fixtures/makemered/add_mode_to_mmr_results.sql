-- ================================================================
-- Make Me Red — mmr_results learns which mode a result belongs to,
-- and how many goes the best took.
-- Run this in: the SHARED Competitive Games Supabase project (the same
-- project Competitive Crosswords uses) → SQL Editor → New query → Run.
--
-- RELEASE GATE (docs/NEXT.md §E "Four- and five-colour stats follow the
-- account"): run this BEFORE deploying the server code that writes
-- `mode` — the code assumes the column exists. ops/smoke.sh proves the
-- live shape after the deploy.
--
-- What it does: hard-mode (four/five-colour) results were deliberately
-- device-local at launch; this widens mmr_results so they can follow
-- the account. One row per (user, date, MODE) now — every existing row
-- is the daily and becomes mode 'three' via the column default, so the
-- backfill is free and instant (Postgres stores the default, no rewrite).
--
-- The primary-key swap is one atomic ALTER. The table is small (weeks
-- old, sparse); the exclusive lock is momentary.
--
-- Idempotent: safe to re-run, per the 2026-09-02 lesson (the only way
-- to discover a migration was never run is an outage).
-- ================================================================

alter table public.mmr_results
  add column if not exists mode text not null default 'three';

-- The chain is closed by the algebra (docs/FIVE.md): a 4×4 board is dead
-- and seven colours impossible, so 'five' is the end. A new value here
-- is a deliberate migration, never an API surprise.
alter table public.mmr_results
  drop constraint if exists mmr_results_mode_check;
alter table public.mmr_results
  add constraint mmr_results_mode_check
  check (mode in ('three', 'four', 'five'));

-- The primary key moves from (user_id, date) to (user_id, date, mode),
-- found by shape, not by name. The table was created as mir_results and
-- renamed later (rename_mir_results_to_mmr_results.sql), and Postgres
-- does not rename a constraint with its table — so on the live project
-- the key is still called mir_results_pkey. A `drop constraint if exists
-- mmr_results_pkey` silently did nothing and the add then failed with
-- 42P16 "multiple primary keys" (2026-09-09, mid-deploy of v1.6.5).
-- Whatever it is called: leave it if it already has the right columns
-- (a re-run), otherwise swap it in one statement each.
do $$
declare
  pk_name text;
  pk_cols text;
begin
  select c.conname,
         (select string_agg(a.attname::text, ',' order by k.ord)
            from unnest(c.conkey) with ordinality as k(attnum, ord)
            join pg_attribute a
              on a.attrelid = c.conrelid and a.attnum = k.attnum)
    into pk_name, pk_cols
    from pg_constraint c
   where c.conrelid = 'public.mmr_results'::regclass
     and c.contype = 'p';
  if pk_cols is distinct from 'user_id,date,mode' then
    if pk_name is not null then
      execute format('alter table public.mmr_results drop constraint %I', pk_name);
    end if;
    alter table public.mmr_results
      add constraint mmr_results_pkey primary key (user_id, date, mode);
  end if;
end
$$;

-- `attempts` is the go on which the stored best was FIRST set — chosen
-- (2026-09-08) over "total goes that day": it freezes once par lands,
-- however much a player replays for fun, and with guiding lights
-- walking anyone home eventually it reads as "the journey to this
-- score". It is a client-claimed progress stat, never anti-cheat
-- material: the goes that matter mostly happen signed out, where the
-- server cannot see them, so the server merely bounds the claim
-- (server/app.ts) and stores it only alongside a result that actually
-- improved the row. Nullable, deliberately: rows written before this
-- column cannot know their go count, and null says so honestly —
-- backfilling 1 would be a guess dressed as data.
alter table public.mmr_results
  add column if not exists attempts integer;
alter table public.mmr_results
  drop constraint if exists mmr_results_attempts_check;
alter table public.mmr_results
  add constraint mmr_results_attempts_check
  check (attempts is null or attempts >= 1);

-- RLS, grants and the (date, verified, time_ms) index are untouched:
-- the own-rows-read policy and server-only writes apply to the new
-- rows exactly as they did to the old.
