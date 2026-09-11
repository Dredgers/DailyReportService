-- ================================================================
-- Competitive Crosswords — Supabase schema
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
-- ================================================================

-- ── Users ──────────────────────────────────────────────────────
-- Mirrors auth.users (managed by Supabase Auth) with app-specific fields.
-- Row is created automatically via trigger on first sign-in.
create table if not exists public.users (
  id            uuid primary key references auth.users(id) on delete cascade,
  email         text,
  display_name  text,
  colour_key    text,                        -- last-used player colour
  created_at    timestamptz default now()
);

alter table public.users enable row level security;

-- Users can read and update only their own row.
create policy "users: own row read"
  on public.users for select
  using (auth.uid() = id);

create policy "users: own row update"
  on public.users for update
  using (auth.uid() = id);

-- Trigger: create a users row the first time someone signs in.
create or replace function public.handle_new_user()
returns trigger language plpgsql security definer as $$
begin
  insert into public.users (id, email, display_name)
  values (
    new.id,
    new.email,
    coalesce(new.raw_user_meta_data->>'full_name', new.email)
  )
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute procedure public.handle_new_user();

-- ── Puzzles ─────────────────────────────────────────────────────
-- One row per distinct puzzle (identified by a stable hash of its content).
create table if not exists public.puzzles (
  id          text primary key,   -- sha256 hex of canonical puzzle JSON
  title       text,
  source_file text,
  created_at  timestamptz default now()
);

alter table public.puzzles enable row level security;

-- Puzzle metadata is public (read-only for everyone).
create policy "puzzles: public read"
  on public.puzzles for select
  using (true);

-- Only the service role (server-side) inserts puzzle rows.

-- ── Game results ─────────────────────────────────────────────────
-- One row per player per completed game.
create table if not exists public.game_results (
  id           bigserial primary key,
  user_id      uuid not null references public.users(id) on delete cascade,
  puzzle_id    text not null references public.puzzles(id),
  mode         text not null,           -- 'competitive' | 'chaotic' | 'solo'
  score        integer not null default 0,
  placement    smallint,                -- 1st, 2nd, … (null for solo)
  player_count smallint,
  duration_s   integer,                 -- seconds from first keypress to completion
  completed_at timestamptz default now()
);

alter table public.game_results enable row level security;

create policy "game_results: own rows read"
  on public.game_results for select
  using (auth.uid() = user_id);

create policy "game_results: own rows insert"
  on public.game_results for insert
  with check (auth.uid() = user_id);

-- ── Puzzles attempted ─────────────────────────────────────────────
-- Tracks whether a user has started / completed a given puzzle.
-- Upserted on start and on completion — one row per (user, puzzle).
create table if not exists public.puzzles_attempted (
  user_id      uuid not null references public.users(id) on delete cascade,
  puzzle_id    text not null references public.puzzles(id),
  mode         text not null,
  started_at   timestamptz default now(),
  completed_at timestamptz,
  best_score   integer,
  primary key (user_id, puzzle_id, mode)
);

alter table public.puzzles_attempted enable row level security;

create policy "puzzles_attempted: own rows read"
  on public.puzzles_attempted for select
  using (auth.uid() = user_id);

create policy "puzzles_attempted: own rows write"
  on public.puzzles_attempted for all
  using (auth.uid() = user_id);
