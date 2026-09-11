-- ================================================================
-- Competitive Crosswords — PostgREST role grants
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- Only needed once. Tables created via SQL editor don't get
-- auto-grants; this wires up the three PostgREST roles.
-- ================================================================

-- service_role: full access to everything (server-side writes)
grant usage  on schema public to service_role;
grant all    on table  public.users               to service_role;
grant all    on table  public.puzzles             to service_role;
grant all    on table  public.game_results        to service_role;
grant all    on table  public.puzzles_attempted   to service_role;
grant usage, select on sequence public.game_results_id_seq to service_role;

-- authenticated: users can read/write their own rows (RLS enforces ownership)
grant usage  on schema public to authenticated;
grant select, update        on table public.users             to authenticated;
grant select                on table public.puzzles           to authenticated;
grant select, insert        on table public.game_results      to authenticated;
grant all                   on table public.puzzles_attempted to authenticated;
grant usage, select on sequence public.game_results_id_seq to authenticated;

-- anon: puzzle metadata is public-readable (e.g. puzzle title display)
grant usage  on schema public to anon;
grant select on table  public.puzzles to anon;
