-- Just enough of Supabase for the two games' SQL to apply on a plain Postgres:
-- the three PostgREST roles, the auth schema with auth.users and auth.uid().
-- Applied first by the integration fixture. Never run this anywhere real.
create role anon nologin;
create role authenticated nologin;
create role service_role nologin;

create schema if not exists auth;

create table if not exists auth.users (
  id                 uuid primary key default gen_random_uuid(),
  email              text,
  raw_user_meta_data jsonb not null default '{}'::jsonb,
  created_at         timestamptz not null default now()
);

-- Supabase's auth.uid() reads the JWT claim; tests never set one, so this yields null and
-- every "own row" policy denies, exactly as it would for a role with no JWT.
create or replace function auth.uid() returns uuid
language sql stable as $$
  select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid
$$;
