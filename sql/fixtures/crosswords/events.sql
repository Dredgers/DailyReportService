-- ================================================================
-- Competitive Crosswords — anonymous product-analytics events
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
--
-- PRIVACY CONTRACT (see docs/dev_docs/ANALYTICS.md and the privacy page):
-- these rows carry NO personal data — no user_id, no IP, no name, no room
-- code, no puzzle content. `session_id` is a random UUID minted in memory per
-- PAGE LOAD (never persisted client-side: no cookie, no localStorage), so it
-- links events WITHIN one visit but cannot follow a person across visits or to
-- their account. Keep it that way: rows are inserted ONLY by the server
-- (service role), so Supabase's own request logs never see a browser IP.
-- ================================================================

create table if not exists public.events (
  id           bigserial primary key,
  name         text not null,            -- allowlisted server-side (funnel step)
  mode         text,                     -- 'competitive' | 'chaotic' | 'solo' | null
  device       text,                     -- 'phone' | 'desktop' (coarse, from viewport)
  player_count smallint,                 -- humans in the room at the event, when known
  session_id   uuid,                     -- per-page-load, memory-only, NOT a user id
  created_at   timestamptz default now()
);

-- Time-series reads (dashboards group by day/name) and a retention sweep.
create index if not exists events_created_at_idx on public.events (created_at);
create index if not exists events_name_idx        on public.events (name);

alter table public.events enable row level security;
-- No RLS policies on purpose: anon/authenticated get NOTHING (not even read).
-- The server writes with the service role (bypasses RLS); dashboards read with a
-- dedicated read-only role (see events_grants.sql), never the browser.

-- ── Retention: 90-day purge ─────────────────────────────────────
-- Counts don't need history; keep the table from becoming an archive. Schedule
-- with pg_cron (Supabase → Database → Extensions → enable pg_cron), or just run
-- the DELETE by hand / from a cron on the box.
create or replace function public.purge_old_events()
returns void language sql as $$
  delete from public.events where created_at < now() - interval '90 days';
$$;

-- Daily at 04:00 UTC, if pg_cron is available. Safe to run more than once.
-- (Comment out if pg_cron isn't enabled; the function above still works manually.)
do $$
begin
  if exists (select 1 from pg_extension where extname = 'pg_cron') then
    perform cron.schedule('purge-old-events', '0 4 * * *', 'select public.purge_old_events()');
  end if;
end $$;
