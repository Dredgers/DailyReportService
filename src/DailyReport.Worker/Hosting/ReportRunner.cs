using System.Diagnostics;
using System.Globalization;
using DailyReport.Core.Abstractions;
using DailyReport.Core.Composition;
using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Render;
using DailyReport.Core.Time;
using DailyReport.Infrastructure.State;
using Microsoft.Extensions.Options;

namespace DailyReport.Worker.Hosting;

public enum RunOutcome
{
    Sent,
    DryRun,
    AlreadySent,
    SendFailed,
}

/// <summary>What a run did, and the report it produced (null when nothing was produced because the date was already sent).</summary>
public sealed record RunResult(RunOutcome Outcome, Report? Report);

/// <summary>
/// One report, end to end: collect every game's metrics and probes with each section isolated, compose, render,
/// archive to disk, send, record. The only thing that fails the run is a failed send; everything else becomes
/// a red line in an email that still goes out.
/// </summary>
public sealed class ReportRunner(
    IOptions<ReportOptions> reportOptions,
    IOptions<GamesOptions> gamesOptions,
    IEnumerable<IGameMetricsProvider> providers,
    IEnumerable<IHealthProbe> probes,
    IEmailSender emailSender,
    IRunStore runStore,
    TimeProvider clock,
    ILogger<ReportRunner> logger)
{
    public async Task<int> RunOnceAsync(Invocation invocation, CancellationToken cancellationToken)
    {
        var options = reportOptions.Value;
        var reportDate = invocation.Date ?? ReportSchedule.ReportDateAt(clock.GetUtcNow(), options.Zone);

        var result = await RunAsync(reportDate, invocation.DryRun, invocation.Force, trigger: "once", cancellationToken);

        // 0: sent, or a dry run with nothing red. 1: the send failed. 2: a dry run whose report has red lines,
        // so a deploy script can show amber without parsing the output.
        return result.Outcome switch
        {
            RunOutcome.SendFailed => 1,
            RunOutcome.DryRun when result.Report is { RedCount: > 0 } => 2,
            _ => 0,
        };
    }

    public async Task<RunResult> RunAsync(DateOnly reportDate, bool dryRun, bool force, string trigger, CancellationToken cancellationToken)
    {
        var options = reportOptions.Value;
        var startedAt = clock.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();

        if (!dryRun && !force && await WasSentSafelyAsync(reportDate, cancellationToken))
        {
            logger.LogInformation("Report for {ReportDate:yyyy-MM-dd} was already sent; not sending again (use --force to resend)", reportDate);
            return new RunResult(RunOutcome.AlreadySent, null);
        }

        logger.LogInformation("Running report for {ReportDate:yyyy-MM-dd} (trigger {Trigger}, dry-run {DryRun})", reportDate, trigger, dryRun);

        var games = gamesOptions.Value.Games;
        var sectionTimeout = TimeSpan.FromSeconds(options.SectionTimeoutSeconds);

        var metricTasks = games.Select(g => CollectMetricsAsync(g, reportDate, sectionTimeout, cancellationToken)).ToList();
        var probeTasks = games.Select(g => RunProbesAsync(g, reportDate, cancellationToken)).ToList();
        await Task.WhenAll(metricTasks.Concat<Task>(probeTasks));

        var inputs = new List<GameInput>();
        var failures = new List<SourceFailure>();
        foreach (var t in metricTasks)
        {
            var (input, sectionFailures) = t.Result;
            inputs.Add(input);
            failures.AddRange(sectionFailures);
        }

        var checks = probeTasks.SelectMany(t => t.Result).ToList();
        var report = ReportComposer.Compose(reportDate, clock.GetUtcNow(), inputs, checks, failures);

        var subject = SubjectLine.For(report);
        var html = HtmlRenderer.Render(report);
        var text = TextRenderer.Render(report);

        // Storage is never allowed to stand between the report and the inbox.
        string archive;
        try
        {
            archive = await ArchiveAsync(options, reportDate, dryRun, html, text, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not archive the report under {StateDirectory}; sending anyway", options.StateDirectory);
            archive = "(archive failed)";
        }

        logger.LogInformation("Report composed in {Elapsed} ms: {Subject}; archived at {Archive}", stopwatch.ElapsedMilliseconds, subject, archive);

        var run = new ReportRun
        {
            ReportDate = SqliteRunStore.Key(reportDate),
            Trigger = trigger,
            StartedAtUnixMs = startedAt.ToUnixTimeMilliseconds(),
            Subject = subject,
            RedCount = report.RedCount,
            WarnCount = report.WarnCount,
        };

        if (dryRun)
        {
            Console.Out.Write(text);
            run.Status = RunStatus.DryRun;
            run.FinishedAtUnixMs = clock.GetUtcNow().ToUnixTimeMilliseconds();
            await TryRecordAsync(run, cancellationToken);
            return new RunResult(RunOutcome.DryRun, report);
        }

        try
        {
            var receipt = await emailSender.SendAsync(new EmailMessage(options.FromName, options.From, options.To, subject, html, text), cancellationToken);
            run.Status = RunStatus.Sent;
            run.ProviderMessageId = receipt.ProviderMessageId;
            run.FinishedAtUnixMs = clock.GetUtcNow().ToUnixTimeMilliseconds();
            await TryRecordAsync(run, cancellationToken);
            logger.LogInformation("Sent report for {ReportDate:yyyy-MM-dd}: {Subject} (id {MessageId})", reportDate, subject, receipt.ProviderMessageId);
            return new RunResult(RunOutcome.Sent, report);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            run.Status = RunStatus.Failed;
            run.Error = $"{ex.GetType().Name}: {ex.Message}";
            run.FinishedAtUnixMs = clock.GetUtcNow().ToUnixTimeMilliseconds();
            await TryRecordAsync(run, cancellationToken);
            logger.LogError(ex, "Sending the report for {ReportDate:yyyy-MM-dd} failed; the rendered report is at {Archive}", reportDate, archive);
            return new RunResult(RunOutcome.SendFailed, report);
        }
    }

    private async Task<bool> WasSentSafelyAsync(DateOnly reportDate, CancellationToken cancellationToken)
    {
        try
        {
            return await runStore.WasSentAsync(reportDate, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not read the run store; assuming {ReportDate:yyyy-MM-dd} was not sent. Twice beats never.", reportDate);
            return false;
        }
    }

    private async Task TryRecordAsync(ReportRun run, CancellationToken cancellationToken)
    {
        try
        {
            await runStore.RecordAsync(run, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not record the {Status} run for {ReportDate}; the email itself was not affected", run.Status, run.ReportDate);
        }
    }

    private async Task<(GameInput Input, IReadOnlyList<SourceFailure> Failures)> CollectMetricsAsync(
        GameOptions game, DateOnly reportDate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var window = new ReportWindow(reportDate, game.Zone);
        var provider = providers.FirstOrDefault(p => p.Provider == game.Metrics.Provider);

        if (provider is null)
        {
            return (new GameInput(game.Key, game.Name, window, []),
                [new SourceFailure(game.Key, "Metrics", $"No metrics provider registered for {game.Metrics.Provider}")]);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var result = await provider.CollectAsync(game, window, timeoutCts.Token);
            return (new GameInput(game.Key, game.Name, window, result.Series), result.Failures);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("{Game}: metrics timed out after {Timeout}", game.Key, timeout);
            return (new GameInput(game.Key, game.Name, window, []),
                [new SourceFailure(game.Key, "Metrics", $"timed out after {timeout.TotalSeconds:0} s")]);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Includes an OperationCanceledException that is not ours (an HttpClient's own timer, say): still a red line, never a crash.
            logger.LogWarning(ex, "{Game}: metrics failed", game.Key);
            return (new GameInput(game.Key, game.Name, window, []),
                [new SourceFailure(game.Key, "Metrics", $"{ex.GetType().Name}: {ex.Message}")]);
        }
    }

    private async Task<IReadOnlyList<CheckResult>> RunProbesAsync(GameOptions game, DateOnly reportDate, CancellationToken cancellationToken)
    {
        var window = new ReportWindow(reportDate, game.Zone);
        var applicable = probes.Where(p => p.AppliesTo(game)).ToList();

        var results = await Task.WhenAll(applicable.Select(async probe =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // Probes promise not to throw; this is the belt to their braces.
                return await probe.RunAsync(game, window, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "{Game}: probe {Probe} threw", game.Key, probe.Name);
                return CheckResult.Error(game.Key, probe.Name, $"{ex.GetType().Name}: {ex.Message}", sw.Elapsed);
            }
        }));

        return results;
    }

    /// <summary>Every rendered report is kept on disk, sent or not, so a failed send loses nothing. Dry runs get their own name so a smoke test never overwrites the report that was actually sent.</summary>
    private static async Task<string> ArchiveAsync(ReportOptions options, DateOnly reportDate, bool dryRun, string html, string text, CancellationToken cancellationToken)
    {
        var dir = Path.Combine(Path.GetFullPath(options.StateDirectory), "reports");
        Directory.CreateDirectory(dir);
        var stem = Path.Combine(dir, reportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + (dryRun ? "-dryrun" : ""));
        await File.WriteAllTextAsync(stem + ".html", html, cancellationToken);
        await File.WriteAllTextAsync(stem + ".txt", text, cancellationToken);
        return stem + ".html";
    }
}
