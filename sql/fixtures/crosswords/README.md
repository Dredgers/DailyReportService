# Database SQL

Run these in the Supabase dashboard → **SQL Editor** → New query → Run, **in order**
(each depends on the previous):

1. **`schema.sql`** — tables (`users`, `puzzles`, `game_results`, `puzzles_attempted`),
   RLS policies, and the new-user trigger.
2. **`grants.sql`** — PostgREST role grants (manually-created tables don't get these
   automatically).
3. **`record_attempt.sql`** — the `record_attempt()` function that records a puzzle
   attempt keeping the best score (atomic `GREATEST`).
4. **`leaderboard.sql`** — adds `game_id` + `is_public` to `game_results` (for the
   speed leaderboards: grouping a co-op team's rows, and a future ranked-points
   filter) plus the supporting indexes.
5. **`ghosts.sql`** — the anonymised `ghosts` table (synthetic-opponent recordings).
   RLS-on with **no** client policies (service-role only); EU-region project only.
   See `docs/MAKING_GHOSTS.md`.
6. **`ghost_opponents.sql`** — adds `game_results.ghost_opponents` (how many ghosts a
   player faced in a race) for honest personal stats. Never used by the points board.
7. **`named_ghosts.sql`** — the **opt-in** consented-ghost lane: adds `users.ghost_consent`
   and a `named_ghosts` table (account-linked, `ON DELETE CASCADE`, RLS-on, service-role
   only). Distinct from the anonymous `ghosts` table. See `docs/MAKING_GHOSTS.md`.
8. **`ranked.sql`** — adds `game_results.ranked` for "one grid, one ranked life": only a
   player's FIRST completion of a puzzle (across ALL modes) counts on the public boards;
   a replay is recorded but `ranked=false`. Set at record time, filtered by every board.
9. **`leaderboard_ghost_win.sql`** — adds `game_results.human_won_vs_ghosts`: for a
   Competitive race that included ghosts, did the leading human top every ghost? The
   Competitive "best time" board lists a ghost race only when this is true. Nullable
   (NULL = rule doesn't apply); safe on a live table.
10. **`fix_client_write_grants.sql`** — revokes the `authenticated` role's INSERT on
    `game_results` and write access on `puzzles_attempted`. Those grants were never
    used by any legitimate client path (the server only ever writes via the
    service-role key) and let a signed-in client forge results or delete its own
    attempt history to farm "first ranked attempt" repeatedly. Run once, any time
    after `grants.sql`. See `docs/dev_docs/GHOST_RATINGS_JOBS.md` Phase 0a.
11. **`ghost_pace.sql`** — adds `pace_spl` (cruise pace, seconds/letter) to `ghosts`
    and `named_ghosts` — the queryable band variable for cross-puzzle ghost
    matching. Argued anonymity exception (pure function of `clue_events` already in
    the row) — see `docs/dev_docs/MAKING_GHOSTS.md`. After running this, backfill
    existing rows with `ops/backfill-pace.mjs`. See
    `docs/dev_docs/GHOST_RATINGS_JOBS.md` Phase 0b.
12. **`ratings.sql`** — the Glicko-1 ratings core: `ratings`, `rating_events`
    (numbers-only replayable audit), `rated_attempts` (the race-start lock) + `rating_snapshot`/
    `rd_snapshot` columns on `named_ghosts`. All service-role only (RLS-on, no client
    grants — the ghosts-table pattern), **including** the `rating_events` bigserial
    sequence grant (without it the audit insert 403s) and the `(game_id, user_id)`
    unique index. Idempotent — safe to re-run. See `docs/dev_docs/GHOST_RATINGS_JOBS.md`
    Phase 3a. (Feature ships from `main`; the table set is applied ahead of it.)

13. **`fix_service_role_grants.sql`** — `revoke all … from anon, authenticated` on the
    six tables that declare themselves service-role only (`ratings`, `rating_events`,
    `rated_attempts`, `ghosts`, `named_ghosts`, `events`) plus their sequences.
    Supabase's project bootstrap runs `alter default privileges in schema public grant
    all on tables to anon, authenticated, service_role`, so the "no client grants by
    design" the files above claim was an absence that was never actually there. **Not
    a live leak** — RLS is on with no policies and neither role has `BYPASSRLS`, so all
    DML was already denied; this closes the part RLS does not cover (`grant all` also
    carries TRUNCATE/REFERENCES/TRIGGER) and keeps the tables shut if a policy is ever
    added. Idempotent; skips absent objects. Run any time after `ratings.sql`.

14. **`ghost_assisted.sql`** — adds `assisted` to `ghosts` and `named_ghosts`: did this
    solve use **Show Selected** (reveal one word for 30/60/120s, Solo only)? The
    recording stays honest — the penalty is folded into its offsets — so this only
    de-prioritises the row in ghost selection (`CC_GHOST_WEIGHT_ASSISTED`, default
    0.25×, never zero). Third argued anonymity exception; see
    `docs/dev_docs/MAKING_GHOSTS.md`. Idempotent — safe to re-run.

**`verify_migrations.sql`** is not part of the sequence — it is a read-only PASS/FAIL
check for whether 10–13 above are actually applied. Run it after any migration, and on
any new environment. It exists because none of these fail loudly: the server never
inspects its own schema, so a missing grant shows up as data that quietly never gets
written. Add a check to it whenever a migration lands.

These are applied manually, not by the server. The server only ever connects with
the service-role key (see `server/db.js`).
