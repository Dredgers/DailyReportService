# Make Me Red — Supabase SQL migrations

These run against the **shared Competitive Games Supabase project** — the
same project Competitive Crosswords (CC) uses, not a project of Make Me
Red's own. Run in the Supabase dashboard → SQL Editor → New query → Run,
**in this order**:

1. `schema.sql` — `public.mmr_results` (one row per user per day,
   `verified` flag — see the comment in the file for exactly what that
   flag means). Does **not** create `public.users` — that table, its RLS,
   and the `handle_new_user()` trigger already exist, created by CC's own
   `server/sql/schema.sql`. Make Me Red just references `public.users(id)`
   as a foreign key, which is the point of sharing the project: one
   identity across every app in the universe.
2. `grants.sql` — PostgREST role grants for `mmr_results` only. `users`
   grants already exist from CC's own `grants.sql`.
3. `rename_mir_results_to_mmr_results.sql` — **one-time, only if you ran
   schema.sql/grants.sql before the app was renamed from "Make It Red"**
   to "Make Me Red" (so a real `mir_results` table already exists in
   your project). Renames it in place — see the file for exactly what it
   does. A project that never had `mir_results` doesn't need this.
4. `mmr_profiles.sql` — `public.mmr_profiles` (display name + synced
   settings). Without it, `/api/profile` 500s and — worse — `DELETE
/api/me` (the GDPR erasure endpoint) fails outright, because account
   deletion clears this table first. **Not optional**, and it now carries its own
   grants (RLS alone is not enough — the server's service-role key needs
   `grant all`, exactly as `mmr_results` does in step 2).
5. `add_mode_to_mmr_results.sql` — widens `mmr_results` to one row per
   (user, date, **mode**) so four/five-colour results can follow the
   account (docs/NEXT.md §E). Existing rows become mode `'three'` via
   the column default. **Release gate**: run it BEFORE deploying the
   server code that writes `mode`; `ops/smoke.sh` probes the column
   after every deploy. Idempotent.

   **This step was missed on the live project and only surfaced on
   2026-09-02**, as `mmr_profiles read failed: Could not find the table
'public.mmr_profiles' in the schema cache` — a 500 for every signed-in
   player. A note here previously ASSERTED the live project already had
   the table; it did not. If you are reading this while setting up a new
   project, run every step and verify each one against the database
   rather than trusting a note like that. The file is idempotent, so
   re-running it to be sure costs nothing.

**Why `mmr_results` and not `results`:** this project's `public` schema is
shared across every app in the Competitive Games universe. Each app
prefixes its own tables (CC's are unprefixed since it was there first —
`game_results`, `puzzles_attempted`, etc. — Make Me Red's are `mmr_`-
prefixed) so table names never collide as more apps join the project.

There's no separate "close the client-write hole" migration here (compare
Competitive Crosswords' `fix_client_write_grants.sql`, written _after_
they discovered signed-in clients could forge scores) — `grants.sql`
never grants `authenticated` insert/update/delete on `mmr_results` in the
first place. The server (service-role key) is the only writer, for every
result: `POST /api/result` (real-time, anti-cheat validated) and
`POST /api/merge` (guest-history import, always written `verified=false`
— see `server/lib/results.ts`). If you ever add a migration that grants
`authenticated` write access to `mmr_results` "to unblock something",
that's almost certainly a mistake — the client should never be able to
write a leaderboard-eligible row directly.

`schema.sql` is not safe to re-run for structural changes (`create table
if not exists` means schema _changes_ need a new migration file, not
edits to this one). `grants.sql` is idempotent.
