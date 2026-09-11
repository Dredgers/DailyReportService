# Daily Report Service — project brief

A scheduled .NET worker that runs each morning and emails a short report on the health and traffic of [makeme.red](https://makeme.red) and [competitivecrosswords.com](https://www.competitivecrosswords.com), with competitivesudokus.com to be added when it launches. The goal is to know, without opening any dashboard, whether yesterday went well.

Games are configured, not hard-coded, so adding a site is a config entry plus whatever game-specific queries it needs.

Built in .NET: a background worker with scheduled jobs and HTTP calls, reading a live Postgres schema through EF Core, with OpenTelemetry for its own monitoring.

## What the report contains

### Traffic
- Arrivals per game, from the Cloudflare analytics API.
- Split by referrer where possible, so a Hacker News or Reddit spike is visible the day it happens.

### Funnel
- Arrivals → puzzle started → completed → returned within seven days.
- These were the stated launch metrics; the report makes them automatic.

### Engagement
- Active streaks and streak breaks.
- Ghost contributions opted in; races run.

### Health
- Today's puzzle actually published.
- Site returns 200.
- Share card renders.
- WebSocket accepts connections.
- Auth callback works.
- Any failed check goes to the top of the email, in red.

### Deltas
- Every number shown against yesterday and the seven-day average, so the report reads as "what changed" rather than a wall of figures.

## Architecture

- **Worker** — .NET 10 `BackgroundService`, scheduled to run once daily. Docker-hosted.
- **Data access** — EF Core with Npgsql, read-only against the Supabase Postgres schema. A separate read-only role; the service never writes to game tables.
- **External sources** — `HttpClient` for Cloudflare analytics, Vercel, and the health probes.
- **Output** — HTML email via a transactional provider; plain-text fallback.
- **Observability** — OpenTelemetry metrics and traces exported to a free Grafana Cloud tier. Optional, but cheap once the service exists, and a monitoring service should monitor itself.
- **Testing** — NUnit. Testcontainers spins up Postgres, seeds a day of events, asserts the report. Health probes tested against a stub server.

## Scope discipline

Start with three numbers and one health check:

1. Arrivals per game
2. Completions per game
3. Seven-day return rate
4. Health: today's puzzle published

Add the rest only when you find yourself wanting them. A weekend for the first version; everything after that is because the report proved useful, not because the list above exists.

## Why it's worth doing

- It's the one thing missing once the games are live: a daily, low-effort view of whether they're working.
- It builds the habit of looking at your own numbers, which is uncomfortable and therefore usually avoided.
- It's the difference between building a product and operating one.
