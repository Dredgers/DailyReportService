# DAILY_REPORT_JOBS.md

Job breakdown for the Daily Report Service, agreed 2026-09-11 from the Q&A over
[`daily-report-service.md`](../../daily-report-service.md). The brief is the *why*;
this is the *what, in what order, by which model*. Where this doc and the brief
disagree (Cloudflare, Vercel, dynamic share cards, an app-side auth callback) this
doc wins, because it was written after reading both game repos.

Each job carries a **recommended model**, picked by the rule the Competitive
Crosswords job lists use: context boundary, not difficulty. Pure modules with a
formula-level spec need almost no repo context (Sonnet 5). Anything that has to
hold both games' schemas and their semantic traps in view at once, or that threads
through the whole run loop, needs whole-picture comprehension (Opus 5).
Judgment-heavy cross-cutting review before anything real is sent: Fable 5.
Transcription: Haiku 4.5.

**Verification is not optional on any of these.** `dotnet test` after each job,
integration tests included (they need Docker Desktop running). Nothing is sent to a
real inbox until R11 has passed.

---

## Status, 2026-09-11 evening

| Job | State | Commit |
|---|---|---|
| R1 skeleton | done | `b9c45e8` |
| R2 model, day maths, composer | done | `4154754` |
| R3 renderers | done | `98b4e0d` |
| R4 read model, `report_ro`, fixtures | done | `d8d718b` |
| R5a / R5b metrics providers | done | `754a7d4` |
| R6 GoatCounter client | done | `dae8ea1` |
| R7 health probes v1 | done | `4462af5` |
| R8 Resend sender | done | `e6f4ebe` |
| R9 orchestration, scheduler, state, health check | done | `a59c1c6` |
| R10 deploy | script + runbook written (`c05c0e2`); not yet run on the box, `report_ro` not yet created, no real send | |
| R11 adversarial review | in progress | |

Test counts at this point: 133 unit, 11 integration (Testcontainers Postgres built from both
games' real SQL). `dotnet run --project src/DailyReport.Worker -- --once --dry-run` works end to
end against the live sites with no secrets: the database and GoatCounter sections go red, the
probes run for real.

### Found on the way, not in the brief

- **Competitive Crosswords' daily has not rotated since 2026-09-04 23:28 Europe/Copenhagen.**
  The very first dry run's "Puzzle published" check failed: `/api/puzzles` reports
  `currentPublishedAt` a week old while `/healthz` says the process has been up 6d 16h with 15
  puzzles loaded. Either `syncPuzzlesAndDaily()` is not promoting (its 30-minute timer, or the
  puzzle-source project is unreachable) or the approved queue is empty and the recycle path is
  failing too. Worth looking at in the crosswords repo before anything else.
- **`cc_dashboard_ro` can read nothing.** Every crosswords table has RLS on; a role with SELECT
  but no policy gets zero rows and no error. `sql/report_ro.sql` here creates a per-table read
  policy for `report_ro` for that reason (and the integration tests prove it reads). The Grafana
  datasource in `CompetitiveCrosswords/ops/grafana` will have the same problem if it is ever
  turned on.
- **GoatCounter's v0 API exposes one number per day** (visitors, a cookieless daily unique), not
  visits and pageviews separately. "Arrivals" for a GoatCounter game means visitors.
- **This Mac had no Docker.** `/usr/local/bin/docker` was a dangling symlink from 2022; Colima
  and the Docker CLI were installed with Homebrew (no sudo) and Testcontainers runs through it.
  The .NET 10 SDK is user-local at `~/.dotnet`; both are on PATH via `~/.zshrc`.
- **The provider contract grew a failure list** (`MetricsResult`): a dead GoatCounter fails its
  own lines while the database lines still report, and vice versa. Decided while building, in
  the spirit of "never skip the email".

## What the two games actually expose

Both games run as Docker containers on one Hetzner box behind Caddy, share one
Supabase project (`qtripphcnsntvnsqkpaf`, Paris) and use Supabase-hosted auth.
Nothing is on Vercel. Cloudflare is DNS-only (grey cloud) for makeme.red and absent
for crosswords, so the Cloudflare analytics API has no data for either site.

| Report item | makeme.red (`Make It Red` repo) | competitivecrosswords.com (`CompetitiveCrosswords` repo) |
|---|---|---|
| Arrivals | GoatCounter (`makemered.goatcounter.com`), hosted, has an API | No page analytics yet. `public.events`: `landing_view` + `invite_joined`, one row per page load. GoatCounter is coming |
| Referrers | GoatCounter `toprefs` | Nothing until GoatCounter lands; referrer is explicitly never collected app-side |
| Puzzle started | `mmr_results.started`, signed-in only | `game_started` event, everyone. **Trap:** `puzzles_attempted.started_at` is written at completion, it is not a start |
| Completed | GoatCounter events `solve`, `solve-four`, `solve-five` (everyone); `mmr_results.completed_at` (signed-in) | `game_finished` event (everyone); `game_results` with `duration_s IS NOT NULL` (signed-in). **Trap:** `duration_s IS NULL` is a give-up |
| Returned in 7 days | Signed-in only, from `mmr_results(user_id, date)` | Signed-in only, from `game_results(user_id, completed_at)`. Anonymous return is unmeasurable by design (per-page-load session id, no cookie) |
| Streaks | Client-side only today; derivable server-side from `mmr_results` | Not built; derivable from `game_results` bucketed by Copenhagen day |
| Ghost opt-ins | n/a | `users.ghost_consent` count, `named_ghosts` count. **Trap:** neither ghost table has any timestamp, so day-over-day needs our own snapshots |
| Races | n/a | `game_started` events with `mode='competitive'` carry `player_count`; `game_results.game_id` groups signed-in rows of one game |
| Puzzle published | `GET /api/puzzle/today` → `{date, puzzleNumber}`, deterministic from the **UTC** date, epoch 2026-08-14 = Red #1 | `GET /api/puzzles` → `currentPublishedAt` = the daily's `used_at`, **Europe/Copenhagen** day. **Trap:** `used_at` is re-stamped on recycle |
| Puzzle supply | n/a (computed) | Separate Supabase project (`PUZZLE_DB_URL`); `puzzles.status='approved' AND used_at IS NULL` is the queue |
| Site up | `GET /healthz` → `{status, uptime_s}`, no DB touch | `GET /healthz` → `{status, uptime_s, rooms, puzzles}` |
| Share card | Static `/og-card.png` (2400×1260) | Static `/og-image.png` (1200×630) |
| WebSocket | None | Same origin as HTTP. `{"type":"queryRoom","roomCode":"<nonexistent>"}` → `{"type":"takenColours","colours":[],"exists":false}` with no side effects |
| Auth | Supabase magic link only; callback is Supabase's | Supabase Google + magic link; callback is Supabase's |
| Rate limits | `/api/*` 120 req/min per IP; `/healthz` exempt | `/api/*` 120 req per 10 s per IP; `/healthz` and static exempt |
| Email | Resend, but only as Supabase's SMTP (dashboard config) | None |
| Uptime | UptimeRobot on `/` and `/healthz` | None |
| Read-only role | None | `cc_dashboard_ro`: SELECT on `events`, `game_results`, `puzzles` only |

Schema for both apps is hand-run SQL in the Supabase dashboard:
`CompetitiveCrosswords/server/sql/*.sql` (14 files, order in its README) and
`Make It Red/server/sql/*.sql`. No migration tool. `events` rows are purged after
90 days, which is fine for 7-day windows.

## Phase 0 — decisions, settled 2026-09-11

- [x] **Cloudflare and Vercel are out.** Arrivals come from GoatCounter where a site
      has it, from `events` where it does not. Crosswords switches by config once its
      GoatCounter site exists.
- [x] **A GoatCounter API token will be created** for `makemered` (Settings → API,
      read-statistics permission). It is the only arrivals and referrer source for
      makeme.red.
- [x] **Two lanes everywhere.** Headline numbers include guests (events / GoatCounter);
      signed-in numbers are shown as their own line, never mixed.
- [x] **Return rate is signed-in only**, and says so in the email. Cohort: players who
      completed on D−8; returned = completed again on any of D−7..D−1. A complete
      window every morning.
- [x] **Each game is reported in its own day.** Crosswords: Europe/Copenhagen.
      makeme.red: UTC. The footer states both. Making them consistent is a game-side
      change, deferred (see below).
- [x] **Deltas:** yesterday vs the day before, vs the mean of the seven days before
      that, and vs the same weekday last week.
- [x] **A streak is a streak.** Consecutive game-days with ≥1 genuine completion, any
      mode, no minimum beyond two days (one day is not a streak). Active = includes
      D−1. Break = streak of ≥2 through D−2 and nothing on D−1.
- [x] **Races include guests; all races count**, ghost-only ones too. Estimator:
      Σ 1/`player_count` over competitive `game_started` events (each human emits one
      event carrying the room's human count). Cross-check against distinct
      competitive `game_id` in `game_results` and show the signed-in figure as the
      second lane. Chaotic games as a separate line.
- [x] **makeme.red modes reported separately** (three / four / five). The `share`
      event count rides along, it is free.
- [x] **Puzzle published** is asserted for both games, plus a low-queue warning for
      crosswords (approved-and-unused count below a threshold, default 3).
- [x] **Share card check follows `og:image`** from the page, validates PNG and
      dimensions, and runs **weekly, not daily**. Phase 2.
- [x] **WebSocket probe** = `queryRoom` on a random nonexistent code, crosswords only.
      Phase 2.
- [x] **Auth path check** = `/api/config` returns keys, Supabase `/auth/v1/health` is
      200, `/api/me` without a token returns 401 not 503. No real sign-in round trip.
      Phase 2.
- [x] **Backup age, TLS expiry, `/healthz` puzzles count**: later.
- [x] **UptimeRobot owns "is it up right now".** Add crosswords `/healthz` there by
      hand. This service answers "did yesterday go well".
- [x] **New Postgres role `report_ro`**, SELECT on exactly the tables listed in R4,
      connected through the session pooler (`aws-0-eu-west-3.pooler.supabase.com:5432`,
      user `report_ro.qtripphcnsntvnsqkpaf`). SQL file in this repo, run by hand in the
      dashboard, per the other repos' convention.
- [x] **The puzzle-source project is read** for the low-queue count only, over
      PostgREST with the existing key (`HEAD …/rest/v1/puzzles?status=eq.approved&used_at=is.null`,
      `Prefer: count=exact`). No second EF context, no second role.
- [x] **EF Core stays**, because it is the CV-relevant skill and this is how it is used
      in industry: a `DbContext` with `NoTracking` for entities and LINQ aggregates,
      `SqlQuery<T>` for the window-function queries (streaks, cohorts). Dapper would be
      leaner for a pure reporting job; the difference here is a few hundred lines and
      no capability.
- [x] **Sibling SQL is copied** into `sql/fixtures/` by `ops/sync-fixtures.sh`; a test
      diffs the copies against the sibling repos when they are present and skips when
      they are not.
- [x] **One binary, two modes.** `--once` runs a report and exits (manual runs, CI,
      first send). Default mode is a `BackgroundService` with a cron schedule.
- [x] **07:00 Europe/Copenhagen.** UTC yesterday is complete by then, the crosswords
      daily has rotated (~00:00–00:30) and the 01:00 puzzle arrival has landed.
- [x] **.NET 10, installed on the Mac** for this project.
- [x] **Resend, from a neutral domain**, to thomashymas@gmail.com for now.
- [x] **Subject:** `Daily report 2026-09-11 · all green` or
      `🔴 2 failed · Daily report 2026-09-11`.
- [x] **Never skip the email.** A source that fails renders its section red with the
      error summary. The run itself only fails if the email cannot be sent.
- [x] **Own state in SQLite** in a Docker volume: run history and daily snapshots of
      timestamp-less counts. This is the only thing the service ever writes.
- [x] **No healthchecks.io ping in v1.** The missing email is the dead-man's switch as
      long as it is read every morning. Add one the first time a silent miss happens.
- [x] **OpenTelemetry is phase 2** (no Grafana Cloud account yet). v1 logs structured
      JSON to stdout; Docker keeps it.

Open:

- [ ] **Hosting.** Recommendation: third container on the Hetzner box, same
      `ops/deploy.sh` shape as the other two apps, no inbound port. `ops/deploy.sh` and
      `docs/DEPLOY.md` are written for that option; the image is the same anywhere.
- [ ] **Sender domain.** "Neutral" needs a domain verified in Resend (DNS records).
      Until one exists, R8 is tested against a stub and the first real send can use
      Resend's onboarding sender to the account's own address.

### Hosting options, for the record

| Option | Cost | Persistent state | Vantage point | Notes |
|---|---|---|---|---|
| **Hetzner box, third container** | Nothing extra | Docker volume | Same box as the games; box-down is UptimeRobot's job | Matches the existing deploy convention. Recommended |
| GitHub Actions scheduled workflow | Free: unlimited minutes on public repos, 2,000/month on private (a daily run uses well under 100) | None: snapshots must live in a committed file, the Actions cache, or Supabase | External | Cron can slip by minutes to an hour at peak; scheduled workflows auto-disable after 60 days without commits; loses the "worker service" item but keeps .NET, EF Core, Testcontainers |
| Azure Container Apps Job / Functions timer | Free tier covers a daily job | Azure Files or Table storage | External | .NET-native, "Azure" on the CV, but a second cloud account to babysit |
| Second small Hetzner VPS | ~€4/month | Its own disk | External | Overkill for one email a day |

The container is the same in every case, so the choice can change later without
touching the code.

## Repo shape

```
DailyReportService.sln
src/DailyReport.Core/             pure: report model, composer, deltas, day maths, renderers
src/DailyReport.Infrastructure/   EF Core contexts + entities, GoatCounter client, probes, Resend client, SQLite store
src/DailyReport.Worker/           Program.cs, options binding, BackgroundService, --once, Dockerfile
tests/DailyReport.Tests/          NUnit: unit tests, golden files, HttpMessageHandler fakes, stub Kestrel server
tests/DailyReport.IntegrationTests/  NUnit + Testcontainers Postgres, applies sql/fixtures, seeds a day, asserts numbers
sql/report_ro.sql                 the read-only role, run by hand in the Supabase dashboard
sql/fixtures/crosswords/*.sql     synced copies of CompetitiveCrosswords/server/sql
sql/fixtures/makemered/*.sql      synced copies of Make It Red/server/sql
sql/fixtures/auth_shim.sql        create schema auth; minimal auth.users so the FKs and trigger apply
ops/sync-fixtures.sh              copies the sibling SQL in; ops/deploy.sh mirrors the other two repos
docs/DEPLOY.md                    runbook; docs/dev_docs/ this file
```

## Config shape

Games are configured, not hard-coded. The *instance* is config; the *queries* are a
provider class per game type. Adding sudokus = a provider (or a parameterised copy
of the crosswords one) + one entry.

```jsonc
"Report": { "SendAtLocalTime": "07:00", "TimeZone": "Europe/Copenhagen", "To": ["…"], "From": "…" },
"Games": [
  {
    "Key": "crosswords", "Name": "Competitive Crosswords",
    "BaseUrl": "https://www.competitivecrosswords.com",
    "DayTimeZone": "Europe/Copenhagen",
    "Metrics": { "Provider": "Crosswords", "Arrivals": "EventsTable" },   // "GoatCounter" later
    "GoatCounter": null,
    "Probes": { "PuzzlePublished": "CrosswordsApiPuzzles", "PuzzleQueueMin": 3,
                "WebSocket": true, "OgImage": { "Cadence": "Weekly" } }
  },
  {
    "Key": "makemered", "Name": "Make Me Red",
    "BaseUrl": "https://makeme.red",
    "DayTimeZone": "UTC",
    "Metrics": { "Provider": "MakeMeRed", "Arrivals": "GoatCounter" },
    "GoatCounter": { "Site": "makemered", "TokenEnv": "GOATCOUNTER_MMR_TOKEN" },
    "Probes": { "PuzzlePublished": "MakeMeRedApiToday", "WebSocket": false,
                "OgImage": { "Cadence": "Weekly" } }
  }
]
```

Secrets by environment variable only: `REPORT_DB_CONNECTION` (`report_ro` via the
pooler), `PUZZLE_DB_URL` + `PUZZLE_DB_KEY`, `GOATCOUNTER_MMR_TOKEN`, `RESEND_API_KEY`.

## Report model

```
Report        { ReportDate, GeneratedAt, Health: CheckResult[], Games: GameSection[], Failures: SourceFailure[] }
GameSection   { GameKey, Name, GameDay (the D−1 date in that game's zone), Zone, Metrics: Metric[] }
Metric        { Key, Label, Lane: All|SignedIn, Value?, DayBefore?, SevenDayMean?, SameWeekdayLastWeek?, Unit }
CheckResult   { GameKey, Name, Status: Pass|Fail|Skipped|Error, Detail, Elapsed }
SourceFailure { Section, Summary }          → rendered red, at the top with failed checks
```

Deltas are computed by the composer, never by the renderer. Failed checks and
source failures sort first. A `Metric` with a null `Value` renders as "no data",
never as 0.

## Phase 1 — the weekend version

Three numbers, one health check, one email: arrivals, completions, 7-day return,
puzzle published. Plus `/healthz`, because it is one line once the probe exists.

| # | Job | Files | Size | Model |
|---|---|---|---|---|
| R1 | **Skeleton.** Install .NET 10 SDK (`dotnet-install.sh` into `~/.dotnet`, no sudo). Solution with the five projects above, `Games`/`Report` options classes with validation on startup, `--once` flag, Dockerfile (non-root, `HEALTHCHECK` on the last-run marker), CLAUDE.md with the test and fixture-sync commands. Done when `dotnet test` is green with one trivial test per project and `dotnet run -- --once` prints an empty report to stdout. | `src/*`, `tests/*`, `Dockerfile`, `CLAUDE.md` | S | Sonnet 5 |
| R2 | **Report model + composer.** The records above; day maths (`GameDay` for a zone, the D−8 cohort window, the seven-day and same-weekday baselines) with tests that cross the Copenhagen DST changes; delta computation; ordering rules. Pure, no I/O. | `Core/Report/*`, `Core/Time/*` | M | Sonnet 5 |
| R3 | **Renderers.** HTML email (inline CSS, one column, works in Gmail) and plain text from the same model. Red banner for failures. Golden-file tests for: all green, one failed check, one dead source, no data. | `Core/Render/*`, `tests/Golden/*` | S | Sonnet 5 |
| R4 | **Read model.** `GamesDbContext` over `events`, `game_results`, `users`, `puzzles_attempted`, `ratings`, `rating_events`, `rated_attempts`, `named_ghosts`, `mmr_results`, `mmr_profiles`; `NoTracking`; entities match the SQL files, not a guess. `sql/report_ro.sql`: login role, SELECT on exactly those tables, `alter default privileges … revoke all` like `cc_dashboard_ro`. `ops/sync-fixtures.sh` + `auth_shim.sql`. Testcontainers fixture applies shim then both repos' SQL in their documented order. Done when the fixture boots clean and a drift test confirms the copies match the siblings. | `Infrastructure/Data/*`, `sql/*`, `ops/sync-fixtures.sh`, `IntegrationTests/Fixtures/*` | M | Opus 5 |
| R5a | **Crosswords metrics.** Arrivals (`landing_view` + `invite_joined`, page loads, labelled so), completions both lanes, signed-in 7-day return, all bucketed by Copenhagen day. Integration tests seed one day of events and results, including a give-up row and a replay row, and assert every number. | `Infrastructure/Games/Crosswords/*` | M | Opus 5 |
| R5b | **Make Me Red metrics.** Completions per mode (signed-in from `mmr_results`; everyone from GoatCounter via R6), signed-in 7-day return, UTC days. | `Infrastructure/Games/MakeMeRed/*` | S | Opus 5 |
| R6 | **GoatCounter client.** Typed `HttpClient`: `stats/total`, `stats/hits` (daily, events flagged), `stats/toprefs`, for a date range; bearer token; retry with backoff; 4 req/s ceiling respected. Fakes via `HttpMessageHandler`. Provides arrivals for makeme.red and, later, crosswords. | `Infrastructure/Sources/GoatCounter/*` | S | Sonnet 5 |
| R7 | **Health probes v1.** `PuzzlePublished`: crosswords `currentPublishedAt` inside today's Copenhagen day; makeme.red `date` == UTC today and `puzzleNumber` == days since 2026-08-14 + 1. `PuzzleQueue`: PostgREST count below `PuzzleQueueMin` → Fail. `Healthz`: 200 and `status:"ok"`. Every probe has a timeout and never throws out. Tests against a stub Kestrel server with canned bodies. | `Infrastructure/Probes/*` | S | Sonnet 5 |
| R8 | **Email sender.** Resend API client, subject rule, HTML + text parts, one retry. Fake handler tests; a `--dry-run` that writes the HTML to disk for eyeballing. | `Infrastructure/Delivery/Resend/*` | S | Sonnet 5 |
| R9 | **Orchestration.** `ReportRunner`: fan out per game and per section, each wrapped so a dead source becomes a `SourceFailure` rather than an exception; per-section timeouts; compose; render; send; record the run in SQLite. `BackgroundService` with Cronos at `07:00 Europe/Copenhagen`, catch-up if the container started after the slot and today's run is missing, idempotent per report date. `--once` reuses the runner. | `Worker/*`, `Infrastructure/State/*` | M | Opus 5 |
| R10 | **Deploy.** `ops/deploy.sh` mirroring the other two repos (build `dr:<sha>`, `--restart=always`, `--env-file /etc/dailyreport/.env`, volume for SQLite, log rotation, no port), `docs/DEPLOY.md`, `report_ro` run in the dashboard, first `--once` send to the real inbox. | `ops/*`, `docs/DEPLOY.md` | S | Sonnet 5 |
| R11 | **Adversarial review, gates the first scheduled send.** Day boundaries in both zones across DST; "yesterday" off-by-one at 07:00; null vs zero; the `duration_s`, `ranked`, `used_at` and `puzzles_attempted.started_at` traps; rate limits; probe side effects; secrets never logged; what happens when Supabase, GoatCounter or Resend is down. | all | S | Fable 5 |

### Job notes

- **R4/R5 traps, restated so they are not rediscovered.** A genuine crosswords
  completion is `duration_s IS NOT NULL`. `ranked=false` rows are replays and still
  count as completions for the report. `puzzles_attempted` is a completion log.
  `named_ghosts` and `ghosts` cannot be dated. `used_at` moves on recycle. `events`
  has no user id, so signed-in lanes always come from `game_results`/`mmr_results`.
- **R7 puzzle-queue threshold** is a warning, not an outage: render amber, not red,
  unless the queue is 0.
- **R9 catch-up rule:** on start, if no run exists for today's report date and the
  local time is past the slot, run immediately. Never run twice for one date.

## Sequencing

```
R1 ─→ R2 ─→ R3
R1 ─→ R4 ─→ R5a, R5b            (R4 is the critical path; it is the only job that cannot be stubbed)
R1 ─→ R6, R7, R8                (independent, all fake- or stub-tested)
R2 + R5 + R6 + R7 + R8 ─→ R9 ─→ R10 ─→ R11 gates the first scheduled send
```

R2/R3 and R6/R7/R8 can run in parallel with R4/R5 because they share only the
`Core` records, which R2 fixes first.

## Phase 2 — when the report has proved useful

| # | Job | Size | Model |
|---|---|---|---|
| R12 | **Referrer split.** GoatCounter `toprefs` per game; top five with counts, "other". Crosswords joins when its GoatCounter site exists (config flip, no code). | S | Sonnet 5 |
| R13 | **Funnel table.** Arrivals → started → completed → returned, both lanes where they exist (`game_started`; `mmr_results.started`). | M | Opus 5 |
| R14 | **Engagement.** Streaks and breaks via window functions in each game's zone; ghost consent and named-ghost totals from SQLite snapshots; races (estimator + cross-check), chaotic games. | M | Opus 5 |
| R15 | **Snapshot store.** SQLite via EF Core: daily point-in-time counts and run history; deltas for timestamp-less metrics. (R9 already creates the file for run history.) | S | Sonnet 5 |
| R16 | **Remaining probes.** Share card via `og:image` (weekly cadence in config), WebSocket `queryRoom` (crosswords), auth path, then backup age, TLS expiry, `/healthz` puzzles count. | M | Sonnet 5 |
| R17 | **OpenTelemetry → Grafana Cloud free tier.** One trace per run, metrics for run duration, probe status, sections failed. Needs an account first. | S | Sonnet 5 |
| R18 | **Sudokus onboarding.** Config entry plus provider. If it shares the crosswords Supabase project, a `game` discriminator column (crosswords "Chunk 13") has to land first or the report cannot tell the rows apart. | S | Sonnet 5 |

## Deferred, documented, don't build

- **Game-day consistency.** Crosswords counts in Copenhagen, makeme.red in UTC. The
  fix is in the games, not the report. Note it in the footer until then.
- **True anonymous return rate.** Needs a persistent id, which the crosswords
  analytics doc deliberately declined. Signed-in return is the number.
- **Real sign-in round trip** through a mailbox. The auth-path proxy is enough.
- **Multiple recipients, per-game emails, Slack/webhook delivery.**
- **healthchecks.io ping.** Add the first time a morning email silently fails to arrive.

## Not in scope, deliberately

- **Writing to any game table.** `report_ro` is SELECT-only; SQLite is the only thing
  this service writes.
- **Uptime alerting.** UptimeRobot owns it; add crosswords `/healthz` there by hand.
- **Dashboards.** The email is the product.
- **Cloudflare anything.** Neither site is proxied and neither wants to be.
