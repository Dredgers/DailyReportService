# DailyReportService

A scheduled .NET 10 worker that emails a short morning report on whether yesterday went well
for [makeme.red](https://makeme.red) and
[competitivecrosswords.com](https://www.competitivecrosswords.com): arrivals, completions,
seven-day return rate, and whether today's puzzle actually published.

- Brief: [`daily-report-service.md`](daily-report-service.md)
- Plan, decisions and job order: [`docs/dev_docs/DAILY_REPORT_JOBS.md`](docs/dev_docs/DAILY_REPORT_JOBS.md)
- Working notes for agents: [`CLAUDE.md`](CLAUDE.md)

```sh
dotnet test tests/DailyReport.Tests                      # no Docker
dotnet run --project src/DailyReport.Worker -- --once --dry-run
```
