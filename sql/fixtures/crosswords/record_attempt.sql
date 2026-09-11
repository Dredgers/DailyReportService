-- ================================================================
-- Competitive Crosswords — record_attempt() upsert function
-- Run this in: Supabase dashboard → SQL Editor → New query → Run
--
-- Atomically records a puzzle attempt, keeping the BEST score rather than
-- overwriting with the latest. The GREATEST happens inside the ON CONFLICT,
-- so concurrent completions can't race to a wrong value (which a
-- read-then-write in app code could).
-- ================================================================

create or replace function public.record_attempt(
  p_user_id   uuid,
  p_puzzle_id text,
  p_mode      text,
  p_score     integer
) returns void
language plpgsql
security definer
as $$
begin
  insert into public.puzzles_attempted (user_id, puzzle_id, mode, completed_at, best_score)
  values (p_user_id, p_puzzle_id, p_mode, now(), p_score)
  on conflict (user_id, puzzle_id, mode) do update
    set completed_at = now(),
        -- greatest() ignores nulls, so the first insert's value wins cleanly.
        best_score   = greatest(public.puzzles_attempted.best_score, excluded.best_score);
end;
$$;

grant execute on function public.record_attempt(uuid, text, text, integer) to service_role;
grant execute on function public.record_attempt(uuid, text, text, integer) to authenticated;
