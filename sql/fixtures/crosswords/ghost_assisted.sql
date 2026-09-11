-- ================================================================
-- Competitive Crosswords — `assisted` on both ghost tables
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (after ghost_pace.sql). Idempotent — safe to re-run.
--
-- Marks a recording whose solve used "Show Selected" (reveal one word for an
-- escalating time penalty — 30s / 60s / 120s, Solo only).
--
-- The recording itself stays honest: the client's ghost recorder accrues the
-- penalty into every subsequent offset (client/ghost-recording.mjs addPenalty),
-- so a revealed word reads as having taken the penalty rather than being solved
-- instantly, and the ghost races at the pace the player was actually credited
-- with. This column therefore does NOT mark bad data — it marks a solve that had
-- help, so selection can prefer a clean one (CC_GHOST_WEIGHT_ASSISTED, default
-- 0.25× the lane weight; never zero, so a puzzle whose only recordings are
-- assisted still fields a ghost).
--
-- ANONYMITY (MAKING_GHOSTS.md "no other columns"): this is a third argued
-- exception, alongside schema_version and pace_spl, and a weaker one than either
-- — a single bit saying "this solver used the hint button", carrying no timing,
-- no identity, and nothing that narrows a solver within the pool. Unlike
-- pace_spl it is NOT derivable from clue_events (a reveal is indistinguishable
-- from a fast solve once the penalty is folded in), which is precisely why it
-- has to be stored rather than computed.
-- ================================================================

alter table public.ghosts
  add column if not exists assisted boolean not null default false;

alter table public.named_ghosts
  add column if not exists assisted boolean not null default false;

-- No new grants/policies: both tables stay service-role only (RLS on, no
-- policies, and the anon/authenticated REVOKE from fix_service_role_grants.sql).

-- ================================================================
-- Verification — expect one row per table, data_type boolean.
-- ================================================================
select table_name, column_name, data_type, column_default
from information_schema.columns
where table_schema = 'public'
  and table_name in ('ghosts', 'named_ghosts')
  and column_name = 'assisted'
order by table_name;
