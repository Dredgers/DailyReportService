# DailyReportService

A scheduled .NET 10 worker that emails a short daily report on the health and traffic of
makeme.red and competitivecrosswords.com. The plan, the settled decisions and the job order
live in `docs/dev_docs/DAILY_REPORT_JOBS.md`; read it before changing anything. The brief
(`daily-report-service.md`) is the *why* and is out of date where the jobs doc says so.

## Toolchain

- The SDK is user-local: `~/.dotnet` (installed with dotnet-install.sh, no sudo). If `dotnet`
  is not found: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`.
- Docker is Colima (`colima start`), needed only by `tests/DailyReport.IntegrationTests`.

## Commands

- `dotnet build`
- `dotnet test tests/DailyReport.Tests` — unit, golden files, stub servers. No Docker.
- `dotnet test tests/DailyReport.IntegrationTests` — Testcontainers Postgres. Needs Docker.
- `dotnet test` — both.
- `dotnet run --project src/DailyReport.Worker -- --once --dry-run` — render today's report to
  stdout and `data/` without sending. Add `--date=YYYY-MM-DD` to re-run a past day.
- `ops/sync-fixtures.sh` — refresh `sql/fixtures/` from the sibling game repos.

## Layering

- `DailyReport.Core` is pure: report model, composer, day maths, renderers. No packages, no I/O.
- `DailyReport.Infrastructure` talks to the world: EF Core over the games' Postgres, GoatCounter,
  health probes, Resend, SQLite state.
- `DailyReport.Worker` hosts: options + validation, `--once`, the scheduler, the Dockerfile.

## Rules that are not negotiable

- **Read-only against game tables.** The `report_ro` role is SELECT-only. SQLite under
  `Report:StateDirectory` is the only thing this service writes.
- **Never skip the email.** A failed source renders its section red with the error; only a
  failed send fails the run.
- **Two lanes, never mixed.** `All` includes guests (events, GoatCounter); `SignedIn` comes from
  `game_results` / `mmr_results`.
- **Each game is reported in its own day zone.** Crosswords: Europe/Copenhagen. Make Me Red: UTC.
- **Semantic traps** (see "Job notes" in the jobs doc): a crosswords completion is
  `duration_s IS NOT NULL`; `puzzles_attempted` is a completion log, not a start log; the ghost
  tables have no timestamps; `used_at` is re-stamped on recycle; `events` has no user id.
- Secrets arrive only as environment variables (`REPORT_DB_CONNECTION`, `PUZZLE_DB_URL`,
  `PUZZLE_DB_KEY`, `GOATCOUNTER_MMR_TOKEN`, `RESEND_API_KEY`, `REPORT_TO`, `REPORT_FROM`).
  Nothing personal is committed: no addresses, no keys.

## Tests

NUnit everywhere. Golden files live in `tests/DailyReport.Tests/Golden` and are compared exactly.
Integration tests apply `sql/fixtures/auth_shim.sql`, then both games' SQL in the order their
READMEs document, seed one day, and assert every number the report shows.
