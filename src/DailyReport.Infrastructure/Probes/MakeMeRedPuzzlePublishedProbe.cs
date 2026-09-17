using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DailyReport.Core.Configuration;
using DailyReport.Core.Model;
using DailyReport.Core.Time;
using Microsoft.Extensions.Options;

namespace DailyReport.Infrastructure.Probes;

/// <summary>
/// GET /api/puzzle/today on Make Me Red. A probe describes now, not the report date: the expected date is today
/// in the game's own zone (UTC) at the moment the probe runs, so a deploy-time dry run at 01:00 Copenhagen is
/// judged against the UTC date the site is actually serving. The expected puzzle number is deterministic from
/// the epoch (Red #1 shipped on <see cref="Epoch"/>).
/// </summary>
public sealed class MakeMeRedPuzzlePublishedProbe(IHttpClientFactory httpClientFactory, IOptions<ReportOptions> reportOptions, TimeProvider clock)
    : ProbeBase(httpClientFactory, reportOptions)
{
    /// <summary>Red #1 shipped on this UTC date; every later puzzleNumber is days-since-this plus one.</summary>
    private static readonly DateOnly Epoch = new(2026, 8, 14);

    public override string Name => ProbeNames.PuzzlePublished;

    public override bool AppliesTo(GameOptions game) => game.Probes.PuzzlePublished == PuzzlePublishedProbe.MakeMeRedApiToday;

    protected override async Task<CheckResult> ProbeAsync(GameOptions game, ReportWindow window, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var client = CreateHttpClient();
        var uri = new Uri(game.BaseUri, "/api/puzzle/today");

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

        var actualDate = root.TryGetProperty("date", out var dateElement) && dateElement.ValueKind == JsonValueKind.String
            ? dateElement.GetString()
            : null;

        int? actualNumber = root.TryGetProperty("puzzleNumber", out var numberElement) && numberElement.TryGetInt32(out var parsedNumber)
            ? parsedNumber
            : null;

        var expectedDate = GameDays.DateIn(clock.GetUtcNow(), game.Zone);
        var expectedDateText = expectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expectedNumber = expectedDate.DayNumber - Epoch.DayNumber + 1;

        var mismatches = new List<string>();

        if (actualDate is null)
        {
            mismatches.Add("date is missing");
        }
        else if (!string.Equals(actualDate, expectedDateText, StringComparison.Ordinal))
        {
            mismatches.Add($"date {actualDate} (expected {expectedDateText})");
        }

        if (actualNumber is null)
        {
            mismatches.Add("puzzleNumber is missing");
        }
        else if (actualNumber != expectedNumber)
        {
            mismatches.Add($"puzzleNumber {actualNumber} (expected {expectedNumber})");
        }

        if (mismatches.Count > 0)
        {
            return CheckResult.Fail(game.Key, Name, string.Join(" · ", mismatches), stopwatch.Elapsed);
        }

        return CheckResult.Pass(game.Key, Name, $"Red #{actualNumber} for {actualDate}", stopwatch.Elapsed);
    }
}
