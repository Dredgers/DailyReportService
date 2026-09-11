using System.Net;
using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Secrets;
using DailyReport.Infrastructure.Sources.GoatCounter;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DailyReport.Tests.Sources.GoatCounter;

public sealed class GoatCounterClientTests
{
    private const string TokenEnv = "TEST_GOATCOUNTER_TOKEN";
    private const string Token = "secret-token-value";

    private static readonly GoatCounterOptions Site = new() { Site = "makemered", TokenEnv = TokenEnv };
    private static readonly DateOnly First = new(2026, 9, 1);
    private static readonly DateOnly Last = new(2026, 9, 3);

    private static GoatCounterClient CreateClient(FakeHttpMessageHandler handler, ISecretReader? secretReader = null) =>
        new(
            new StubHttpClientFactory(handler),
            Options.Create(new GoatCounterClientOptions()),
            secretReader ?? new FakeSecretReader(new Dictionary<string, string> { [TokenEnv] = Token }),
            NullLogger<GoatCounterClient>.Instance);

    [Test]
    public async Task Request_carries_bearer_token_and_hits_the_sites_host()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"total":0,"total_events":0,"total_utc":0,"stats":[]}""");
        var client = CreateClient(handler);

        await client.GetDailyTotalsAsync(Site, First, Last, CancellationToken.None);

        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        var request = handler.Requests[0];
        Assert.Multiple(() =>
        {
            Assert.That(request.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo(Token));
            Assert.That(request.RequestUri?.Host, Is.EqualTo("makemered.goatcounter.com"));
            Assert.That(request.RequestUri?.AbsolutePath, Is.EqualTo("/api/v0/stats/total"));
        });
    }

    [Test]
    public async Task Totals_request_sends_start_and_end_as_yyyy_MM_dd()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"total":0,"total_events":0,"total_utc":0,"stats":[]}""");
        var client = CreateClient(handler);

        await client.GetDailyTotalsAsync(Site, First, Last, CancellationToken.None);

        var query = QueryHelpers.ParseQuery(handler.Requests[0].RequestUri!.Query);
        Assert.Multiple(() =>
        {
            Assert.That(query["start"].ToString(), Is.EqualTo("2026-09-01"));
            Assert.That(query["end"].ToString(), Is.EqualTo("2026-09-03"));
        });
    }

    [Test]
    public async Task Hits_request_sets_daily_flag_and_a_generous_limit()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"hits":[],"more":false,"total":0}""");
        var client = CreateClient(handler);

        await client.GetDailyEventsAsync(Site, First, Last, CancellationToken.None);

        var request = handler.Requests[0];
        var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
        Assert.Multiple(() =>
        {
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/v0/stats/hits"));
            Assert.That(query["daily"].ToString(), Is.EqualTo("true"));
            Assert.That(int.Parse(query["limit"].ToString()), Is.GreaterThanOrEqualTo(20));
        });
    }

    [Test]
    public async Task Toprefs_request_sends_the_requested_limit()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"more":false,"stats":[]}""");
        var client = CreateClient(handler);

        await client.GetTopReferrersAsync(Site, First, Last, 5, CancellationToken.None);

        var request = handler.Requests[0];
        var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
        Assert.Multiple(() =>
        {
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/v0/stats/toprefs"));
            Assert.That(query["limit"].ToString(), Is.EqualTo("5"));
        });
    }

    [Test]
    public async Task Totals_parse_a_realistic_payload_and_zero_fill_missing_days_in_order()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {
              "total": 20,
              "total_events": 4,
              "total_utc": 20,
              "stats": [
                {"day": "2026-09-01", "daily": 12, "hourly": [1,2,3]},
                {"day": "2026-09-03", "daily": 8, "hourly": [4,5,6]}
              ]
            }
            """);
        var client = CreateClient(handler);

        var days = await client.GetDailyTotalsAsync(Site, First, Last, CancellationToken.None);

        Assert.That(days, Is.EqualTo(new[]
        {
            new GoatCounterDay(new DateOnly(2026, 9, 1), 12, 12),
            new GoatCounterDay(new DateOnly(2026, 9, 2), 0, 0),
            new GoatCounterDay(new DateOnly(2026, 9, 3), 8, 8),
        }));
    }

    [Test]
    public async Task Events_exclude_non_event_paths_and_zero_fill_per_event_across_the_range()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {
              "hits": [
                {"path": "/", "path_id": 1, "count": 100, "event": false, "stats": [
                    {"day": "2026-09-01", "daily": 40}, {"day": "2026-09-02", "daily": 35}, {"day": "2026-09-03", "daily": 25}
                ]},
                {"path": "/solve", "path_id": 2, "count": 15, "event": true, "stats": [
                    {"day": "2026-09-01", "daily": 10}, {"day": "2026-09-03", "daily": 5}
                ]},
                {"path": "/share", "path_id": 3, "count": 8, "event": true, "stats": [
                    {"day": "2026-09-02", "daily": 8}
                ]}
              ],
              "more": false,
              "total": 3
            }
            """);
        var client = CreateClient(handler);

        var events = await client.GetDailyEventsAsync(Site, First, Last, CancellationToken.None);

        Assert.That(events, Is.EqualTo(new[]
        {
            new GoatCounterEventDay("solve", new DateOnly(2026, 9, 1), 10),
            new GoatCounterEventDay("solve", new DateOnly(2026, 9, 2), 0),
            new GoatCounterEventDay("solve", new DateOnly(2026, 9, 3), 5),
            new GoatCounterEventDay("share", new DateOnly(2026, 9, 1), 0),
            new GoatCounterEventDay("share", new DateOnly(2026, 9, 2), 8),
            new GoatCounterEventDay("share", new DateOnly(2026, 9, 3), 0),
        }));
    }

    [Test]
    public async Task Events_follow_the_more_flag_by_excluding_already_seen_path_ids()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {
              "hits": [
                {"path": "/solve", "path_id": 2, "count": 10, "event": true, "stats": [{"day": "2026-09-01", "daily": 10}]}
              ],
              "more": true,
              "total": 3
            }
            """);
        handler.Enqueue(HttpStatusCode.OK, """
            {
              "hits": [
                {"path": "/share", "path_id": 3, "count": 8, "event": true, "stats": [{"day": "2026-09-02", "daily": 8}]}
              ],
              "more": false,
              "total": 3
            }
            """);
        var client = CreateClient(handler);

        var events = await client.GetDailyEventsAsync(Site, First, Last, CancellationToken.None);

        Assert.That(handler.Requests, Has.Count.EqualTo(2));
        var secondQuery = QueryHelpers.ParseQuery(handler.Requests[1].RequestUri!.Query);
        Assert.Multiple(() =>
        {
            Assert.That(secondQuery["exclude_paths"].ToString(), Is.EqualTo("2"));
            Assert.That(events.Select(e => e.EventName).Distinct(), Is.EquivalentTo(new[] { "solve", "share" }));
        });
    }

    [Test]
    public async Task Referrers_parse_and_respect_the_limit()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {
              "more": true,
              "stats": [
                {"id": "1", "name": "Hacker News", "count": 50},
                {"id": "2", "name": "(direct)", "count": 30},
                {"id": "3", "name": "Reddit", "count": 10}
              ]
            }
            """);
        var client = CreateClient(handler);

        var referrers = await client.GetTopReferrersAsync(Site, First, Last, 2, CancellationToken.None);

        Assert.That(referrers, Is.EqualTo(new[]
        {
            new GoatCounterReferrer("Hacker News", 50),
            new GoatCounterReferrer("(direct)", 30),
        }));
    }

    [Test]
    public void Missing_token_throws_MissingSecretException_never_zeros()
    {
        var handler = new FakeHttpMessageHandler();
        var client = CreateClient(handler, new FakeSecretReader(new Dictionary<string, string>()));

        Assert.That(
            async () => await client.GetDailyTotalsAsync(Site, First, Last, CancellationToken.None),
            Throws.InstanceOf<MissingSecretException>());
        Assert.That(handler.Requests, Is.Empty, "no request should be sent without a token");
    }

    [Test]
    public void Non_2xx_response_throws_GoatCounterException_with_the_status_and_body()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.InternalServerError, "internal boom");
        var client = CreateClient(handler);

        var ex = Assert.ThrowsAsync<GoatCounterException>(
            async () => await client.GetDailyTotalsAsync(Site, First, Last, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(ex.BodyExcerpt, Is.EqualTo("internal boom"));
            Assert.That(ex.Message, Does.Contain("500"));
        });
    }

    [Test]
    public void Malformed_json_throws_GoatCounterException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "{ this is not valid json");
        var client = CreateClient(handler);

        Assert.ThrowsAsync<GoatCounterException>(
            async () => await client.GetDailyTotalsAsync(Site, First, Last, CancellationToken.None));
    }

    [Test]
    public void Body_excerpt_is_truncated_to_500_characters()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.BadGateway, new string('x', 900));
        var client = CreateClient(handler);

        var ex = Assert.ThrowsAsync<GoatCounterException>(
            async () => await client.GetDailyTotalsAsync(Site, First, Last, CancellationToken.None));

        Assert.That(ex!.BodyExcerpt, Has.Length.EqualTo(500));
    }
}
