-- ================================================================
-- Competitive Crosswords — strip anon/authenticated from the
-- service-role-only tables
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- (any time after ratings.sql; safe before it too — absent objects are skipped)
--
-- ghosts.sql, named_ghosts.sql, events_grants.sql and ratings.sql each declare
-- their tables "service-role only … no authenticated/anon grants" — but none of
-- them REVOKE, they only GRANT to service_role. Supabase's project bootstrap
-- runs
--   alter default privileges in schema public
--     grant all on tables    to anon, authenticated, service_role;
--   alter default privileges in schema public
--     grant all on sequences to anon, authenticated, service_role;
-- so every table `postgres` creates in `public` is granted to the client roles
-- automatically. "No grants by design" described an absence that was never
-- there. Confirmed against the production DB on 2026-09-02.
--
-- NOT a live data leak. RLS is enabled with NO policies on all of these tables
-- and neither anon nor authenticated has BYPASSRLS, so every SELECT / INSERT /
-- UPDATE / DELETE was already denied. What this closes is the part RLS does not
-- cover: `grant all` also carries TRUNCATE, REFERENCES and TRIGGER — the same
-- point fix_client_write_grants.sql:29-31 makes about grants.sql's `grant all`.
-- It also means the tables stay shut if a policy is ever added, or if RLS is
-- toggled off during a future migration.
--
-- Cannot break a working path: RLS already denies these roles all DML, so
-- nothing functioning today can depend on the grants being removed here.
--
-- Idempotent. Skips anything absent (events.sql is optional and not part of the
-- numbered order) — a bare REVOKE on a missing relation would abort the whole
-- script, since the Supabase SQL editor runs it as one transaction.
--
-- Deliberately per-table rather than
--   alter default privileges in schema public revoke all on tables from …
-- Changing the schema-wide default would silently deny future tables that
-- legitimately need client access, surfacing months later as a confusing
-- permission-denied. Per-table matches what the rest of server/sql/ does.
-- ================================================================

do $$
declare obj text;
begin
  foreach obj in array array[
    'ratings', 'rating_events', 'rated_attempts', 'ghosts', 'named_ghosts', 'events'
  ] loop
    if to_regclass('public.' || obj) is null then
      raise notice 'skip (absent): %', obj;
    else
      execute format('revoke all on table public.%I from anon, authenticated', obj);
      raise notice 'revoked table: %', obj;
    end if;
  end loop;

  -- Sequences carry their own default-privilege grant; `revoke on TABLE` does
  -- not reach them — the mirror of the grant bug noted at ratings.sql:82.
  foreach obj in array array['rating_events_id_seq', 'events_id_seq'] loop
    if to_regclass('public.' || obj) is null then
      raise notice 'skip (absent): %', obj;
    else
      execute format('revoke all on sequence public.%I from anon, authenticated', obj);
      raise notice 'revoked sequence: %', obj;
    end if;
  end loop;
end $$;

-- ================================================================
-- Verification 1 — table grants. Expect ZERO rows.
-- ================================================================
select table_name, grantee, privilege_type
from information_schema.role_table_grants
where table_schema = 'public'
  and grantee in ('anon', 'authenticated')
  and table_name in ('ratings', 'rating_events', 'rated_attempts',
                     'ghosts', 'named_ghosts', 'events')
order by table_name, grantee, privilege_type;

-- ================================================================
-- Verification 2 — sequence grants. Run SEPARATELY (the SQL editor shows only
-- the last result set). Expect anon = false and authenticated = false on every
-- row. Also worth confirming the premise of "not a live leak": both roles must
-- report bypass_rls = false in
--   select rolname, rolbypassrls from pg_roles
--   where rolname in ('anon', 'authenticated');
-- ================================================================
select s.seq, p.priv,
       has_sequence_privilege('anon',          s.oid, p.priv) as anon,
       has_sequence_privilege('authenticated', s.oid, p.priv) as authenticated
from (
  select c.oid, c.relname as seq
  from pg_class c join pg_namespace n on n.oid = c.relnamespace
  where n.nspname = 'public' and c.relkind = 'S'
    and c.relname in ('rating_events_id_seq', 'events_id_seq')
) s
cross join (values ('USAGE'), ('SELECT'), ('UPDATE')) p(priv)
order by s.seq, p.priv;
