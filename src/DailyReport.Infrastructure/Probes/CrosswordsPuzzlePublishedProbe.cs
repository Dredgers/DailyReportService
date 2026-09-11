using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using Microsoft.Extensions.Options;

namespace DailyReport.Infrastructure.Probes;

/// <summary>
/// GET /api/puzzles on crosswords. <c>currentPublishedAt</c> is the daily's <c>used_at</c>, stamped when it
/// was promoted around local midnight Europe/Copenhagen — it must fall inside today's game day by the time
/// the 07:00 report runs. <c>used_at</c> is re-stamped on recycle, so a stale value here means the rotation
/// did not happen, not that nobody looked.
/// </summary>
public sealed class CrosswordsPuzzlePublishedProbe(IHttpClientFactory httpClientFactory, IOptions<ReportOptions> reportOptions)
    : ProbeBase(httpClientFactory, reportOptions)
{
    public override string Name => "Puzzle published";

    public override bool AppliesTo(GameOptions game) => game.Probes.PuzzlePublished == PuzzlePublishedProbe.CrosswordsApiPuzzles;

    protected override async Task<CheckResult> ProbeAsync(GameOptions game, ReportWindow window, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var client = CreateHttpClient();
        var uri = new Uri(game.BaseUri, "/api/puzzles");

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
        var rawPublishedAt = root.TryGetProperty("currentPublishedAt", out var publishedElement) && publishedElement.ValueKind == JsonValueKind.String
            ? publishedElement.GetString()
            : null;

        if (rawPublishedAt is null)
        {
            return CheckResult.Fail(game.Key, Name, "currentPublishedAt is missing", stopwatch.Elapsed);
        }

        if (!DateTimeOffset.TryParse(rawPublishedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var publishedAt))
        {
            return CheckResult.Fail(game.Key, Name, $"currentPublishedAt '{rawPublishedAt}' is not a timestamp", stopwatch.Elapsed);
        }

        var id = root.TryGetProperty("currentId", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()
            : null;

        // Custom .NET format strings treat ':' as the culture's time separator, not a literal colon —
        // format with InvariantCulture explicitly so this never depends on the host machine's locale.
        var local = TimeZoneInfo.ConvertTime(publishedAt, game.Zone);
        var localText = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var detail = $"published {localText} {game.DayTimeZone} · id {id ?? "(missing)"}";

        // The report runs the morning of D; the daily rotates around 00:00-00:30 local, so "today" is D itself.
        var todayRange = window.RangeOf(window.ReportDate);

        return todayRange.Contains(publishedAt)
            ? CheckResult.Pass(game.Key, Name, detail, stopwatch.Elapsed)
            : CheckResult.Fail(game.Key, Name, detail, stopwatch.Elapsed);
    }
}
