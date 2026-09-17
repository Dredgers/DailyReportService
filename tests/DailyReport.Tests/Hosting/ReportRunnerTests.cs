using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Infrastructure.State;
using DailyReport.Tests.State;
using DailyReport.Worker.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DailyReport.Tests.Hosting;

public sealed class ReportRunnerTests
{
    // 07:00 CEST on Friday 11 September 2026.
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 11);

    private TempStateFactory state = null!;
    private FakeEmailSender email = null!;
    private SqliteRunStore store = null!;

    [SetUp]
    public void SetUp()
    {
        state = new TempStateFactory();
        email = new FakeEmailSender();
        store = new SqliteRunStore(state);
    }

    [TearDown]
    public void TearDown() => state.Dispose();

    private ReportRunner Build(IEnumerable<IGameMetricsProvider>? providers = null, IEnumerable<IHealthProbe>? probes = null, int sectionTimeoutSeconds = 60, TimeProvider? clock = null)
    {
        var report = new ReportOptions
        {
            To = ["someone@example.invalid"], From = "report@example.invalid", FromName = "Daily Report",
            StateDirectory = state.Directory, SectionTimeoutSeconds = sectionTimeoutSeconds,
        };
        var games = new GamesOptions
        {
            Games =
            [
                new GameOptions { Key = "crosswords", Name = "Competitive Crosswords", BaseUrl = "https://www.competitivecrosswords.com", DayTimeZone = "Europe/Copenhagen", Metrics = new MetricsOptions { Provider = MetricsProvider.Crosswords, Arrivals = ArrivalsSource.EventsTable } },
                new GameOptions { Key = "makemered", Name = "Make Me Red", BaseUrl = "https://makeme.red", DayTimeZone = "UTC", Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed, Arrivals = ArrivalsSource.GoatCounter }, GoatCounter = new GoatCounterOptions { Site = "makemered", TokenEnv = "X" } },
            ],
        };

        providers ??= [FakeProvider.Healthy(MetricsProvider.Crosswords), FakeProvider.Healthy(MetricsProvider.MakeMeRed)];
        probes ??= [FakeProbe.Passing("Site up")];

        return new ReportRunner(Options.Create(report), Options.Create(games), providers, probes, email, store, clock ?? new FixedClock(Now), NullLogger<ReportRunner>.Instance);
    }

    private static Invocation Once(bool dryRun = false, bool force = false, DateOnly? date = null) => new(true, dryRun, date, force, false);

    [Test]
    public async Task Happy_path_sends_a_green_report_and_records_the_run()
    {
        var exit = await Build().RunOnceAsync(Once(), default);

        Assert.Multiple(async () =>
        {
            Assert.That(exit, Is.EqualTo(0));
            Assert.That(email.Sent, Has.Count.EqualTo(1));
            Assert.That(email.Sent[0].Subject, Is.EqualTo("Daily report 2026-09-11 · all green"));
            Assert.That(email.Sent[0].To, Is.EqualTo(new[] { "someone@example.invalid" }));
            Assert.That(email.Sent[0].Html, Does.Contain("Competitive Crosswords").And.Contain("Make Me Red"));
            Assert.That(email.Sent[0].Text, Does.Contain("Europe/Copenhagen").And.Contain("UTC"));
            Assert.That(await store.WasSentAsync(Today, default), Is.True);
            Assert.That(File.Exists(Path.Combine(state.Directory, "reports", "2026-09-11.html")), Is.True, "archived html");
            Assert.That(File.Exists(Path.Combine(state.Directory, "reports", "2026-09-11.txt")), Is.True, "archived text");
        });
    }

    [Test]
    public async Task A_throwing_provider_becomes_a_red_line_and_the_email_still_goes()
    {
        var runner = Build(providers: [FakeProvider.Throwing(MetricsProvider.Crosswords, "connection refused"), FakeProvider.Healthy(MetricsProvider.MakeMeRed)]);

        var outcome = (await runner.RunAsync(Today, dryRun: false, force: false, "test", default)).Outcome;

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(RunOutcome.Sent));
            Assert.That(email.Sent[0].Subject, Is.EqualTo("🔴 1 failed · Daily report 2026-09-11"));
            Assert.That(email.Sent[0].Text, Does.Contain("connection refused"));
            Assert.That(email.Sent[0].Text, Does.Contain("Make Me Red"), "the healthy game still reports");
        });
    }

    [Test]
    public async Task A_hanging_provider_is_cut_off_at_the_section_timeout()
    {
        var runner = Build(providers: [FakeProvider.Hanging(MetricsProvider.Crosswords), FakeProvider.Healthy(MetricsProvider.MakeMeRed)], sectionTimeoutSeconds: 1);

        var started = DateTime.UtcNow;
        var outcome = (await runner.RunAsync(Today, dryRun: false, force: false, "test", default)).Outcome;

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(RunOutcome.Sent));
            Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(10)));
            Assert.That(email.Sent[0].Text, Does.Contain("timed out after 1 s"));
        });
    }

    [Test]
    public async Task A_missing_provider_is_a_red_line_not_a_crash()
    {
        var runner = Build(providers: [FakeProvider.Healthy(MetricsProvider.Crosswords)]);

        await runner.RunAsync(Today, dryRun: false, force: false, "test", default);

        Assert.That(email.Sent[0].Text, Does.Contain("No metrics provider registered for MakeMeRed"));
    }

    [Test]
    public async Task A_probe_that_throws_is_reported_as_an_error_check()
    {
        var runner = Build(probes: [FakeProbe.Throwing("Puzzle published"), FakeProbe.Failing("Site up")]);

        await runner.RunAsync(Today, dryRun: false, force: false, "test", default);

        Assert.Multiple(() =>
        {
            // Two games × two probes: two Fail + two Error.
            Assert.That(email.Sent[0].Subject, Is.EqualTo("🔴 4 failed · Daily report 2026-09-11"));
            Assert.That(email.Sent[0].Text, Does.Contain("probe blew up"));
        });
    }

    [Test]
    public async Task A_failed_send_is_exit_code_1_with_the_report_archived_and_the_failure_recorded()
    {
        email.Throw = new HttpRequestException("resend down");

        var exit = await Build().RunOnceAsync(Once(), default);

        Assert.Multiple(async () =>
        {
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(await store.WasSentAsync(Today, default), Is.False);
            Assert.That(await store.LastSuccessAsync(default), Is.Null);
            Assert.That(File.Exists(Path.Combine(state.Directory, "reports", "2026-09-11.html")), Is.True);
        });
    }

    [Test]
    public async Task Dry_run_sends_nothing_and_does_not_count_as_sent()
    {
        var outcome = (await Build().RunAsync(Today, dryRun: true, force: false, "once", default)).Outcome;

        Assert.Multiple(async () =>
        {
            Assert.That(outcome, Is.EqualTo(RunOutcome.DryRun));
            Assert.That(email.Sent, Is.Empty);
            Assert.That(await store.WasSentAsync(Today, default), Is.False);
            Assert.That(await store.LastSuccessAsync(default), Is.Not.Null, "a dry run still proves the pipeline works");
            Assert.That(File.Exists(Path.Combine(state.Directory, "reports", "2026-09-11-dryrun.txt")), Is.True, "a dry run never overwrites the sent report's archive");
        });
    }

    [Test]
    public async Task A_paused_check_is_skipped_with_its_reason_and_never_reaches_the_site()
    {
        var ran = false;
        var wouldFail = new FakeProbe(
            "Puzzle published",
            _ => true,
            (g, _, _) => { ran = true; return Task.FromResult(CheckResult.Fail(g.Key, "Puzzle published", "13 days stale", TimeSpan.Zero)); });

        var game = new GameOptions
        {
            Key = "crosswords", Name = "Competitive Crosswords", BaseUrl = "https://www.competitivecrosswords.com", DayTimeZone = "Europe/Copenhagen",
            Metrics = new MetricsOptions { Provider = MetricsProvider.Crosswords, Arrivals = ArrivalsSource.EventsTable },
            Probes = new ProbeOptions { Paused = { ["Puzzle published"] = "puzzle supply on hold" } },
        };
        var report = new ReportOptions { To = ["someone@example.invalid"], From = "report@example.invalid", StateDirectory = state.Directory };
        var runner = new ReportRunner(
            Options.Create(report), Options.Create(new GamesOptions { Games = [game] }),
            [FakeProvider.Healthy(MetricsProvider.Crosswords)], [wouldFail, FakeProbe.Passing("Site up")],
            email, store, new FixedClock(Now), NullLogger<ReportRunner>.Instance);

        var result = await runner.RunAsync(Today, dryRun: false, force: false, "test", default);

        Assert.Multiple(() =>
        {
            Assert.That(ran, Is.False, "a paused check must not reach the site at all");
            Assert.That(result.Report!.Status, Is.EqualTo(ReportStatus.Green), "a known pause is not an alarm");
            Assert.That(email.Sent[0].Subject, Is.EqualTo("Daily report 2026-09-11 · all green"));
            Assert.That(email.Sent[0].Text, Does.Contain("paused: puzzle supply on hold"), "but it is still visible, with the reason");
        });
    }

    [Test]
    public async Task A_dry_run_with_red_lines_exits_2_so_a_deploy_can_show_amber()
    {
        var green = await Build().RunOnceAsync(Once(dryRun: true), default);
        var red = await Build(probes: [FakeProbe.Failing("Site up")]).RunOnceAsync(Once(dryRun: true), default);

        Assert.That((green, red), Is.EqualTo((0, 2)));
    }

    [Test]
    public async Task An_unwritable_archive_does_not_stop_the_email()
    {
        // Point the archive at a path that is a file, so creating "<it>/reports" throws.
        var blocker = Path.Combine(state.Directory, "blocker");
        File.WriteAllText(blocker, "not a directory");
        var report = new ReportOptions { To = ["someone@example.invalid"], From = "report@example.invalid", StateDirectory = blocker };
        var games = new GamesOptions { Games = [new GameOptions { Key = "makemered", Name = "Make Me Red", BaseUrl = "https://makeme.red", DayTimeZone = "UTC", Metrics = new MetricsOptions { Provider = MetricsProvider.MakeMeRed } }] };
        var runner = new ReportRunner(Options.Create(report), Options.Create(games), [FakeProvider.Healthy(MetricsProvider.MakeMeRed)], [FakeProbe.Passing("Site up")], email, store, new FixedClock(Now), NullLogger<ReportRunner>.Instance);

        var result = await runner.RunAsync(Today, dryRun: false, force: false, "test", default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Sent));
            Assert.That(email.Sent, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task The_same_report_date_is_not_sent_twice_unless_forced()
    {
        var runner = Build();

        var first = (await runner.RunAsync(Today, dryRun: false, force: false, "schedule", default)).Outcome;
        var second = (await runner.RunAsync(Today, dryRun: false, force: false, "catch-up", default)).Outcome;
        var forced = (await runner.RunAsync(Today, dryRun: false, force: true, "once", default)).Outcome;

        Assert.Multiple(() =>
        {
            Assert.That((first, second, forced), Is.EqualTo((RunOutcome.Sent, RunOutcome.AlreadySent, RunOutcome.Sent)));
            Assert.That(email.Sent, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Report_date_defaults_to_the_local_date_in_the_report_zone()
    {
        // 22:30Z on the 10th is 00:30 on the 11th in Copenhagen: the report is for the 11th (about the 10th).
        var runner = Build(clock: new FixedClock(new DateTimeOffset(2026, 9, 10, 22, 30, 0, TimeSpan.Zero)));

        await runner.RunOnceAsync(Once(), default);

        Assert.That(email.Sent[0].Subject, Does.Contain("2026-09-11"));
    }

    [Test]
    public async Task An_explicit_date_reruns_a_past_report()
    {
        await Build().RunOnceAsync(Once(date: new DateOnly(2026, 9, 1)), default);

        Assert.Multiple(() =>
        {
            Assert.That(email.Sent[0].Subject, Does.Contain("2026-09-01"));
            Assert.That(email.Sent[0].Text, Does.Contain("Monday 31 August 2026"));
        });
    }
}
