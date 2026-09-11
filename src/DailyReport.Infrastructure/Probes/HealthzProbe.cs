using System.Diagnostics;
using System.Text.Json;
using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using Microsoft.Extensions.Options;

namespace DailyReport.Infrastructure.Probes;

/// <summary>
/// GET /healthz on every game. Both sites reply <c>{"status":"ok","uptime_s":N}</c>; crosswords adds
/// <c>rooms</c> and <c>puzzles</c>. Not rate-limited, so this is safe to run every morning without care.
/// </summary>
public sealed class HealthzProbe(IHttpClientFactory httpClientFactory, IOptions<ReportOptions> reportOptions)
    : ProbeBase(httpClientFactory, reportOptions)
{
    public override string Name => "Site up";

    public override bool AppliesTo(GameOptions game) => true;

    protected override async Task<CheckResult> ProbeAsync(GameOptions game, ReportWindow window, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var client = CreateHttpClient();
        var uri = new Uri(game.BaseUri, "/healthz");

        using var response = await SendAsync(client, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return CheckResult.Fail(game.Key, Name, $"{(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd(), stopwatch.Elapsed);
        }

        using var document = TryParseJson(body);
        if (document is null)
        {
            return CheckResult.Fail(game.Key, Name, $"non-JSON body: {Truncate(body)}", stopwatch.Elapsed);
        }

        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString()
            : null;

        if (!string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return CheckResult.Fail(game.Key, Name, $"status \"{status ?? "(missing)"}\"", stopwatch.Elapsed);
        }

        var detail = "200 ok";

        if (root.TryGetProperty("uptime_s", out var uptimeElement) && uptimeElement.TryGetDouble(out var uptimeSeconds))
        {
            detail += $" · up {FormatUptime(uptimeSeconds)}";
        }

        if (root.TryGetProperty("rooms", out var roomsElement) && roomsElement.TryGetInt64(out var rooms))
        {
            detail += $" · rooms {rooms}";
        }

        if (root.TryGetProperty("puzzles", out var puzzlesElement) && puzzlesElement.TryGetInt64(out var puzzles))
        {
            detail += $" · puzzles {puzzles}";
        }

        return CheckResult.Pass(game.Key, Name, detail, stopwatch.Elapsed);
    }

    private static string FormatUptime(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);

        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays}d {span.Hours}h";
        }

        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }

        if (span.TotalMinutes >= 1)
        {
            return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        }

        return $"{span.Seconds}s";
    }
}
