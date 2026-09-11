# Deploying the Daily Report Service

One container on the existing Hetzner box next to Competitive Crosswords (`cc`, port 8080) and
Make Me Red (`mmr`, port 8081). This one opens no port. It wakes at 07:00 Europe/Copenhagen,
reads the shared Supabase project as `report_ro`, asks GoatCounter and the two sites a few
questions, and sends one email through Resend. State (a SQLite file and an archive of every
rendered report) lives in the Docker volume `dr-data`.

Hosting is still an open decision in `dev_docs/DAILY_REPORT_JOBS.md`; this runbook is written
for the recommended option. The image is the same wherever it runs.

## Redeploy

```sh
ssh box
~/deploy-dr.sh
```

The script pulls `main`, builds `dr:<sha>`, restarts the container, waits for the scheduler's
first log line, then runs `--once --dry-run` inside the container and prints the report to your
terminal. Nothing is sent. Every red line in that output is either a real finding about the games
or a missing secret; fix the secret and redeploy, or read the finding and go fix the game.

To send today's report right away instead of waiting for 07:00:

```sh
docker exec dr dotnet DailyReport.Worker.dll --once
```

The runner refuses to send the same report date twice; add `--force` to resend, and
`--date=YYYY-MM-DD` to re-run a past day (the database queries are date-bounded, so a past day
is exact; the probes always describe *now*).

## 1. One-time setup

### 1a. The read-only database role

In the Supabase dashboard of the shared project, SQL editor, paste `sql/report_ro.sql` after
replacing `CHANGE_ME_STRONG_PASSWORD` with a real password. Run it. The two SELECTs at the end
should list twelve tables with `SELECT` and twelve `report_ro: read` policies. Both games'
tables are covered; the policies matter because every table has row level security on and a
role with no policy sees zero rows without any error.

Build the connection string for `.env` from the **session pooler** (Project Settings →
Database → Connection string → Session mode, port 5432), swapping the user for
`report_ro.<project-ref>`:

```
Host=aws-0-eu-west-3.pooler.supabase.com;Port=5432;Database=postgres;Username=report_ro.<project-ref>;Password=…;SSL Mode=Require
```

### 1b. GoatCounter

On `makemered.goatcounter.com`: Settings → API → new token with **read statistics**. Also set
the site's time zone to **UTC** (Settings → Site) so GoatCounter's days line up with Make Me
Red's UTC game day; otherwise arrivals and completions are bucketed on Copenhagen days while
the signed-in numbers are not.

When Competitive Crosswords gets its own GoatCounter site, add a token, flip its
`Metrics:Arrivals` to `GoatCounter` and add a `GoatCounter` block in `appsettings.json`, commit,
redeploy. No code changes.

### 1c. Resend

An API key from the Resend dashboard, and a **verified sending domain** for `REPORT_FROM`.
Which domain is the one open decision left in the jobs doc. Until it is settled, Resend's
onboarding sender can deliver to the account owner's own address for a first real send.

### 1d. The puzzle-library key

Crosswords' puzzle queue lives in its separate Supabase project. Copy `PUZZLE_DB_URL` and
`PUZZLE_DB_KEY` from the crosswords `.env` on the box (`/etc/cc/.env`). The service only ever
issues one `count=exact` request against it.

### 1e. The box

```sh
# Secrets file, readable by you and root only, same convention as the other two apps.
sudo mkdir -p /etc/dailyreport
sudo cp ~/DailyReportService/.env.example /etc/dailyreport/.env
sudo chown root:$USER /etc/dailyreport/.env && sudo chmod 640 /etc/dailyreport/.env
sudoedit /etc/dailyreport/.env          # fill every value

# Checkout + deploy script.
git clone https://github.com/Dredgers/DailyReportService.git ~/DailyReportService
cp ~/DailyReportService/ops/deploy.sh ~/deploy-dr.sh && chmod +x ~/deploy-dr.sh
~/deploy-dr.sh
```

### 1f. UptimeRobot

This service does not watch uptime. Add `https://www.competitivecrosswords.com/healthz` to the
existing UptimeRobot account by hand; makeme.red already has monitors.

## 2. Verify

```sh
docker logs --tail 20 dr                                  # "Next report at 2026-09-12T05:00:00Z (2026-09-12 07:00 Europe/Copenhagen)"
docker exec dr dotnet DailyReport.Worker.dll --healthcheck   # healthy: ...
docker exec dr dotnet DailyReport.Worker.dll --once --dry-run | head -40
docker run --rm -v dr-data:/data alpine ls -la /data /data/reports   # the SQLite file and the archive
```

The Docker `HEALTHCHECK` runs `--healthcheck` every 30 minutes and turns the container
unhealthy after 26 hours without a successful run. Nothing acts on that yet; `docker ps` shows
it, and a missing morning email is the signal that matters.

## 3. Rollback

The deploy prints the previously running image. To go back:

```sh
docker rm -f dr && docker run -d --name dr --restart=always \
  --log-opt max-size=10m --log-opt max-file=3 \
  --env-file /etc/dailyreport/.env -v dr-data:/data dr:<previous-sha>
```

The SQLite schema is created by the application and has no migrations, so any build can read
the volume.

## 4. Changing what is reported

Games are configuration in `src/DailyReport.Worker/appsettings.json`: key, name, origin, day
zone, metrics provider, arrivals source, probes. A change is a commit and a redeploy, the same
as the other two apps. A brand-new game with a schema of its own also needs an
`IGameMetricsProvider` implementation (see `src/DailyReport.Infrastructure/Games/`) and, if
it shares the database, its tables added to `sql/report_ro.sql` and re-run in the dashboard.

## 5. Environment variable reference

| Variable | Required | Meaning |
|---|---|---|
| `REPORT_DB_CONNECTION` | yes | Npgsql connection string for `report_ro` via the session pooler |
| `PUZZLE_DB_URL`, `PUZZLE_DB_KEY` | for the queue probe | Crosswords' puzzle-library project; missing → that check reads Error, the rest of the report is unaffected |
| `GOATCOUNTER_MMR_TOKEN` | for makeme.red traffic | Read-statistics token; missing → makeme.red's GoatCounter lines go red, signed-in lines still report |
| `RESEND_API_KEY` | yes | Missing → the report is rendered and archived, the send fails, exit code 1 |
| `REPORT_TO` | yes | Comma-separated recipients |
| `REPORT_FROM`, `REPORT_FROM_NAME` | yes / no | Sender; domain must be verified in Resend |
| `Report__SendAtLocalTime`, `Report__TimeZone` | no | Defaults 07:00, Europe/Copenhagen |
| `Report__StateDirectory` | no | `/data` in the image |
