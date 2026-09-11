-- The Daily Report Service's database identity: a login role that can SELECT the tables the
-- report reads and nothing else. Run in the Supabase dashboard (SQL editor) of the shared
-- project, as the postgres user, after replacing the password. Idempotent: safe to re-run
-- after either game adds a table the report needs (add it to both lists below).
--
-- Why the policies: every one of these tables has row level security enabled, and RLS
-- denies by default to any role that has no policy, table ownership or BYPASSRLS. A plain
-- GRANT SELECT would compile fine and return zero rows for ever. Supabase's postgres user
-- cannot hand out BYPASSRLS, so each table gets a permissive read policy scoped to this
-- role only. Other roles' policies are unaffected.
--
-- Connection string for REPORT_DB_CONNECTION (session pooler, port 5432):
--   Host=aws-0-eu-west-3.pooler.supabase.com;Port=5432;Database=postgres;
--   Username=report_ro.<project-ref>;Password=<the password below>;SSL Mode=Require

do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'report_ro') then
    create role report_ro login password 'CHANGE_ME_STRONG_PASSWORD';
  end if;
end $$;

grant usage on schema public to report_ro;

-- Crosswords. Three tables get column-level SELECT: users carries emails, the ghost tables carry the
-- recordings themselves (clue_events) and a display name. The report counts rows and reads flags; it
-- never needs those columns, so it cannot read them. (A column added by a future migration is not
-- covered until it is added here; the integration fixture selects every mapped column and will say so.)
grant select (id, created_at, ghost_consent) on public.users to report_ro;
grant select on table public.puzzles          to report_ro;
grant select on table public.game_results     to report_ro;
grant select on table public.puzzles_attempted to report_ro;
grant select on table public.events           to report_ro;
grant select on table public.ratings          to report_ro;
grant select on table public.rating_events    to report_ro;
grant select on table public.rated_attempts   to report_ro;
grant select (ghost_id, puzzle_id, schema_version, pace_spl, assisted) on public.ghosts to report_ro;
grant select (ghost_id, user_id, puzzle_id, schema_version, pace_spl, rating_snapshot, rd_snapshot, assisted) on public.named_ghosts to report_ro;
-- Make Me Red
grant select on table public.mmr_results      to report_ro;
grant select on table public.mmr_profiles     to report_ro;

-- Belt and braces. Supabase's bootstrap grants future tables to anon/authenticated/service_role by name,
-- never to this role, so these mostly state the intent: report_ro gets nothing it was not given above.
alter default privileges in schema public revoke all on tables from report_ro;
alter default privileges in schema public revoke all on sequences from report_ro;
alter default privileges in schema public revoke all on functions from report_ro;

do $$
declare t text;
begin
  foreach t in array array[
    'users', 'puzzles', 'game_results', 'puzzles_attempted', 'events',
    'ratings', 'rating_events', 'rated_attempts', 'ghosts', 'named_ghosts',
    'mmr_results', 'mmr_profiles'
  ] loop
    execute format('drop policy if exists "report_ro: read" on public.%I', t);
    execute format('create policy "report_ro: read" on public.%I for select to report_ro using (true)', t);
  end loop;
end $$;

-- Verify: nine tables with table-level SELECT, three with column-level SELECT, twelve policies.
select table_name, privilege_type
from information_schema.role_table_grants
where grantee = 'report_ro' and table_schema = 'public'
order by table_name;

select table_name, string_agg(column_name, ', ' order by column_name) as columns
from information_schema.role_column_grants
where grantee = 'report_ro' and table_schema = 'public'
group by table_name
order by table_name;

select tablename, policyname
from pg_policies
where policyname = 'report_ro: read'
order by tablename;
