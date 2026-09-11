using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DailyReport.Infrastructure.Sources.GoatCounter;

// Research notes (2026-09-11), so the next person does not have to re-derive this from a Go source tree:
//
// Sources read: https://www.goatcounter.com/help/api (prose docs, auth, rate limit), the OpenAPI 2.0 document
// at https://www.goatcounter.com/api.json (authoritative for parameter names, types and response shapes;
// api2.html/api.html are a JS-rendered RapiDoc viewer over the same document and did not add anything a
// fetcher without a browser could see), and the reference implementation at
// https://github.com/arp242/goatcounter (handlers/api.go for query binding, handlers/api_test.go for the
// date format the project's own tests actually send, since the spec's "format: date-time" is misleading).
//
// Auth: "Authorization: Bearer <token>" (HTTP Basic with an empty username also works but is not used here).
// 401 = missing/bad key, 403 = key lacks the "read statistics" permission. The token is minted per site in
// the GoatCounter dashboard (Settings → API); this client resolves it fresh on every call via
// ISecretReader.Require(site.TokenEnv) rather than caching it, so a revoked token surfaces immediately as a
// MissingSecretException/401 instead of a stale success.
//
// Rate limit: 4 requests/second, reported on every response via X-Rate-Limit-Limit, X-Rate-Limit-Remaining
// and X-Rate-Limit-Reset (seconds until the window resets). The docs do not spell out the status code for
// going over, but it is the conventional 429 Too Many Requests; that, plus 5xx and connect/timeout failures,
// is exactly what AddStandardResilienceHandler() (registered in GoatCounterServiceCollectionExtensions)
// retries with jittered backoff, so this class does not implement its own retry loop.
//
// Endpoints used, all under the site's own subdomain (https://{site}.goatcounter.com):
//   GET /api/v0/stats/total   start, end (query params, "yyyy-MM-dd" — see date-format note below) →
//       { total, total_events, total_utc, stats: [{ day, daily, hourly, weekly, monthly }] }. One entry per
//       day with traffic; there is no daily/group parameter here because the per-day breakdown is always
//       returned. No pagination.
//   GET /api/v0/stats/hits    start, end, limit (max 100, default 20), daily=true (or group=day; "daily" is
//       documented "deprecated, identical to group=day" but still accepted and simplest for our case),
//       exclude_paths (comma-separated path IDs — the pagination mechanism; see below) →
//       { hits: [{ path, path_id, count, event, stats: [{ day, daily }] }], more, total }. `event` flags
//       paths GoatCounter's dashboard would show under "Events" (configured per-site by the site owner to be
//       "solve", "solve-four", "share", etc.) rather than a page view.
//   GET /api/v0/stats/toprefs  really /api/v0/stats/{page} with page=toprefs; start, end, limit (max 100) →
//       { stats: [{ id, name, count }], more }. `count` is visitors referred, `name` is GoatCounter's display
//       name for the referrer ("Hacker News", "(direct)", a bare hostname, ...).
//
// Pagination: only /stats/hits paginates, and not with an offset — the "more" flag means call again with
// exclude_paths set to every path_id already seen, which asks the server to skip them and return the next
// page. We loop with a hard cap (GoatCounter sites here have a handful of event paths, not hundreds) rather
// than trust "more" never lies.
//
// Date format: the OpenAPI spec marks start/end as "format: date-time, should be rounded to the hour" (Go's
// time.Time round-trips as RFC 3339 by default), but the project's own handler tests
// (handlers/api_test.go) call these endpoints with plain "start=2020-06-17&end=2020-06-19" — that is what we
// send. The "day" field in every per-day stat is read leniently (GoatCounterDayConverter) in case a future
// version answers with a full timestamp instead.
//
// Visits vs. pageviews: the v0 API's only count is "visitors" (HitList.count / HitListStat.daily); there is
// no separate "count_unique" or pageview figure anywhere in api.json. GetDailyTotalsAsync therefore puts the
// same GoatCounter visitor count in both GoatCounterDay.Visits and GoatCounterDay.Pageviews — they are not
// independently measured by this API version, so treat them as one number under two names, not two lanes.
//
// Missing token: ISecretReader.Require throws MissingSecretException before any request is sent, so a
// misconfigured site fails loudly (never as a page of zeros dressed up as real data).

/// <summary>
/// <see cref="IGoatCounterClient"/> over GoatCounter's public v0 stats API. See the file header for the
/// research this implementation is based on: endpoint shapes, the visits/pageviews conflation, pagination and
/// rate-limit behaviour.
/// </summary>
public sealed class GoatCounterClient : IGoatCounterClient
{
    internal const string HttpClientName = "goatcounter";

    private const int MaxPageSize = 100;
    private const int MaxHitsPages = 20;
    private const int BodyExcerptLength = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoatCounterClientOptions _options;
    private readonly ISecretReader _secretReader;
    private readonly ILogger<GoatCounterClient> _logger;

    public GoatCounterClient(
        IHttpClientFactory httpClientFactory,
        IOptions<GoatCounterClientOptions> options,
        ISecretReader secretReader,
        ILogger<GoatCounterClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _secretReader = secretReader;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GoatCounterDay>> GetDailyTotalsAsync(GoatCounterOptions site, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(site);
        RequireValidRange(first, last);

        var query = RangeQuery(first, last);
        var response = await SendAsync<GoatCounterTotalResponseDto>(site, "api/v0/stats/total", query, cancellationToken).ConfigureAwait(false);

        var byDay = IndexByDay(response.Stats);

        var days = new List<GoatCounterDay>();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var count = byDay.GetValueOrDefault(day, 0);
            // No separate pageview figure in this API version; see the file header. Both fields carry the
            // same visitor count.
            days.Add(new GoatCounterDay(day, count, count));
        }

        return days;
    }

    public async Task<IReadOnlyList<GoatCounterEventDay>> GetDailyEventsAsync(GoatCounterOptions site, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(site);
        RequireValidRange(first, last);

        var hits = await GetAllHitsAsync(site, first, last, cancellationToken).ConfigureAwait(false);

        var results = new List<GoatCounterEventDay>();
        foreach (var hit in hits)
        {
            if (!hit.Event || string.IsNullOrEmpty(hit.Path))
            {
                continue;
            }

            var eventName = hit.Path.TrimStart('/');
            var byDay = IndexByDay(hit.Stats);

            for (var day = first; day <= last; day = day.AddDays(1))
            {
                results.Add(new GoatCounterEventDay(eventName, day, byDay.GetValueOrDefault(day, 0)));
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<GoatCounterReferrer>> GetTopReferrersAsync(GoatCounterOptions site, DateOnly first, DateOnly last, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(site);
        RequireValidRange(first, last);
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be positive.");
        }

        var query = RangeQuery(first, last);
        query["limit"] = Math.Min(limit, MaxPageSize).ToString(CultureInfo.InvariantCulture);

        var response = await SendAsync<GoatCounterStatsResponseDto>(site, "api/v0/stats/toprefs", query, cancellationToken).ConfigureAwait(false);

        return (response.Stats ?? [])
            .Take(limit)
            .Select(stat => new GoatCounterReferrer(stat.Name ?? stat.Id ?? "(unknown)", stat.Count ?? 0))
            .ToList();
    }

    private async Task<List<GoatCounterHitDto>> GetAllHitsAsync(GoatCounterOptions site, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        var hits = new List<GoatCounterHitDto>();
        var excludePaths = new List<string>();

        for (var page = 0; page < MaxHitsPages; page++)
        {
            var query = RangeQuery(first, last);
            query["daily"] = "true";
            query["limit"] = MaxPageSize.ToString(CultureInfo.InvariantCulture);
            if (excludePaths.Count > 0)
            {
                query["exclude_paths"] = string.Join(',', excludePaths);
            }

            var response = await SendAsync<GoatCounterHitsResponseDto>(site, "api/v0/stats/hits", query, cancellationToken).ConfigureAwait(false);
            var pageHits = response.Hits ?? [];
            hits.AddRange(pageHits);

            if (!response.More || pageHits.Count == 0)
            {
                break;
            }

            excludePaths.AddRange(pageHits.Select(hit => hit.PathId.ToString(CultureInfo.InvariantCulture)));
        }

        return hits;
    }

    private async Task<TResponse> SendAsync<TResponse>(
        GoatCounterOptions site,
        string path,
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken)
    {
        var token = _secretReader.Require(site.TokenEnv);
        var requestUri = BuildRequestUri(site, path, query);

        _logger.LogDebug("GoatCounter request {RequestUri}", requestUri);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new GoatCounterException(response.StatusCode, requestUri, Excerpt(body));
        }

        try
        {
            return JsonSerializer.Deserialize<TResponse>(body, SerializerOptions)
                   ?? throw new GoatCounterException(response.StatusCode, requestUri, "Response body was \"null\".");
        }
        catch (JsonException ex)
        {
            throw new GoatCounterException(response.StatusCode, requestUri, Excerpt(body), ex);
        }
    }

    private Uri BuildRequestUri(GoatCounterOptions site, string path, IReadOnlyDictionary<string, string> query)
    {
        var baseUrl = _options.BaseUrlPattern.Replace("{site}", site.Site, StringComparison.Ordinal);
        var builder = new UriBuilder(new Uri(new Uri(baseUrl, UriKind.Absolute), path))
        {
            Query = string.Join('&', query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")),
        };
        return builder.Uri;
    }

    private static Dictionary<string, string> RangeQuery(DateOnly first, DateOnly last) => new(StringComparer.Ordinal)
    {
        ["start"] = first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["end"] = last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static Dictionary<DateOnly, int> IndexByDay(IEnumerable<GoatCounterHitListStatDto>? stats)
    {
        var byDay = new Dictionary<DateOnly, int>();
        foreach (var stat in stats ?? [])
        {
            if (stat.Day is { } day)
            {
                byDay[day] = stat.Daily ?? 0;
            }
        }

        return byDay;
    }

    private static void RequireValidRange(DateOnly first, DateOnly last)
    {
        if (last < first)
        {
            throw new ArgumentOutOfRangeException(nameof(last), last, "last must not precede first.");
        }
    }

    private static string Excerpt(string body) => body.Length <= BodyExcerptLength ? body : body[..BodyExcerptLength];
}
