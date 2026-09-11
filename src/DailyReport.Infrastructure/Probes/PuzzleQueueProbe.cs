using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using Microsoft.Extensions.Options;

namespace DailyReport.Infrastructure.Probes;

/// <summary>
/// Crosswords' approved-and-unused puzzle count, read from the separate puzzle-library Supabase project
/// over PostgREST (<c>PuzzleSource:Url</c>/<c>Key</c>, i.e. PUZZLE_DB_URL/PUZZLE_DB_KEY). The query returns
/// no rows we care about: with <c>Prefer: count=exact</c> and a zero-width <c>Range</c>, the queue length is
/// the total in the <c>Content-Range: 0-0/N</c> (or <c>*/N</c>) response header.
/// </summary>
public sealed class PuzzleQueueProbe(
    IHttpClientFactory httpClientFactory,
    IOptions<ReportOptions> reportOptions,
    IOptions<PuzzleSourceOptions> puzzleSourceOptions)
    : ProbeBase(httpClientFactory, reportOptions)
{
    public override string Name => "Puzzle queue";

    public override bool AppliesTo(GameOptions game) => game.Probes.PuzzleQueueMin is not null;

    protected override async Task<CheckResult> ProbeAsync(GameOptions game, ReportWindow window, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var source = puzzleSourceOptions.Value;
        if (string.IsNullOrWhiteSpace(source.Url) || string.IsNullOrWhiteSpace(source.Key))
        {
            return CheckResult.Error(game.Key, Name, "PUZZLE_DB_URL / PUZZLE_DB_KEY not set", stopwatch.Elapsed);
        }

        var uri = new Uri($"{source.Url.TrimEnd('/')}/rest/v1/puzzles?select=id&status=eq.approved&used_at=is.null");
        var client = CreateHttpClient();

        using var response = await SendAsync(client, () => BuildRequest(uri, source.Key), cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return CheckResult.Fail(game.Key, Name, $"{(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd(), stopwatch.Elapsed);
        }

        var header = response.Content.Headers.TryGetValues("Content-Range", out var values) ? values.FirstOrDefault() : null;

        if (!TryParseCount(header, out var count))
        {
            return CheckResult.Fail(game.Key, Name, $"unparseable Content-Range: '{header ?? "(missing)"}'", stopwatch.Elapsed);
        }

        var min = game.Probes.PuzzleQueueMin!.Value;

        if (count == 0)
        {
            return CheckResult.Fail(game.Key, Name, "queue empty: tomorrow recycles an old puzzle", stopwatch.Elapsed);
        }

        if (count < min)
        {
            return CheckResult.Warn(game.Key, Name, $"{count} approved puzzles queued (min {min})", stopwatch.Elapsed);
        }

        return CheckResult.Pass(game.Key, Name, $"{count} approved puzzles queued", stopwatch.Elapsed);
    }

    private static HttpRequestMessage BuildRequest(Uri uri, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        request.Headers.TryAddWithoutValidation("Range-Unit", "items");
        request.Headers.TryAddWithoutValidation("Range", "0-0");
        return request;
    }

    /// <summary>The queue length is whatever follows the last '/' in "0-0/N" or "*/N".</summary>
    private static bool TryParseCount(string? contentRange, out int count)
    {
        count = 0;
        if (string.IsNullOrWhiteSpace(contentRange))
        {
            return false;
        }

        var slash = contentRange.LastIndexOf('/');
        if (slash < 0 || slash == contentRange.Length - 1)
        {
            return false;
        }

        return int.TryParse(contentRange[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }
}
