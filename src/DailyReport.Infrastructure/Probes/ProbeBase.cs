using System.Diagnostics;
using System.Text.Json;
using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using Microsoft.Extensions.Options;

namespace DailyReport.Infrastructure.Probes;

/// <summary>
/// Shared plumbing for every health probe: a running stopwatch, a timeout linked to the caller's token,
/// the named HTTP client, and a blanket exception handler. A probe never throws out of
/// <see cref="IHealthProbe.RunAsync"/> — any exception, the timeout's own <see cref="OperationCanceledException"/>
/// included, becomes a <see cref="CheckStatus.Error"/> result instead: "we could not check" is itself the finding.
/// </summary>
public abstract class ProbeBase(IHttpClientFactory httpClientFactory, IOptions<ReportOptions> reportOptions) : IHealthProbe
{
    /// <summary>The named client registered by <see cref="ProbeServiceCollectionExtensions.AddHealthProbes"/>.</summary>
    private const string HttpClientName = "probes";

    public abstract string Name { get; }

    public abstract bool AppliesTo(GameOptions game);

    public async Task<CheckResult> RunAsync(GameOptions game, ReportWindow window, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(reportOptions.Value.ProbeTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            return await ProbeAsync(game, window, stopwatch, linked.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Includes OperationCanceledException from the timeout above: a probe that could not complete
            // is not a crash of the run, it is the finding "we do not know" for this one check.
            return CheckResult.Error(game.Key, Name, $"{ex.GetType().Name}: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <summary>The probe body. Build the final <see cref="CheckResult"/> with <paramref name="stopwatch"/>.Elapsed.</summary>
    protected abstract Task<CheckResult> ProbeAsync(GameOptions game, ReportWindow window, Stopwatch stopwatch, CancellationToken cancellationToken);

    protected HttpClient CreateHttpClient() => httpClientFactory.CreateClient(HttpClientName);

    /// <summary>
    /// Sends one request, with a single retry after one second on a connection-level failure (no status code
    /// at all — refused, reset, DNS). A non-2xx response is not retried: that is a fact about the site, not
    /// a network hiccup, so the probe should fail fast on it instead.
    /// </summary>
    protected static async Task<HttpResponseMessage> SendAsync(HttpClient client, Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(requestFactory(), cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            return await client.SendAsync(requestFactory(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Parses a JSON body, or null on anything that is not valid JSON — a Fail, not a thrown exception.</summary>
    protected static JsonDocument? TryParseJson(string content)
    {
        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    protected static string Truncate(string value, int maxLength = 120) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
