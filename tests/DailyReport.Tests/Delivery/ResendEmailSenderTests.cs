using System.Net;
using System.Text.Json;
using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Delivery.Resend;
using DailyReport.Infrastructure.Secrets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DailyReport.Tests.Delivery;

public sealed class ResendEmailSenderTests
{
    private static readonly EmailMessage Message = new(
        FromName: "Daily Report",
        From: "report@example.invalid",
        To: ["thomashymas@gmail.com", "second@example.invalid"],
        Subject: "Daily report 2026-09-11 · all green",
        Html: "<p>all green</p>",
        Text: "all green");

    private static ResendEmailSender BuildSender(RecordingHttpMessageHandler handler, string apiKey = "re_test_key", string baseUrl = "https://api.resend.com")
    {
        var options = Options.Create(new ResendOptions { ApiKey = apiKey, BaseUrl = baseUrl });
        return new ResendEmailSender(new SingleClientHttpClientFactory(handler), options, NullLogger<ResendEmailSender>.Instance);
    }

    [Test]
    public async Task Sends_the_expected_request_shape()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"49a3999c-0ce1-4ea6-ab68-afcd6dc2e794"}""");
        var sender = BuildSender(handler, apiKey: "re_abc123", baseUrl: "https://api.resend.com/");

        await sender.SendAsync(Message, CancellationToken.None);

        var request = handler.LastRequest!;
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.RequestUri, Is.EqualTo(new Uri("https://api.resend.com/emails")));
            Assert.That(request.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo("re_abc123"));
            Assert.That(request.Headers.GetValues("Idempotency-Key").Single(), Does.Match("^dr-[0-9a-f]{32}$"));
        });

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("from").GetString(), Is.EqualTo("Daily Report <report@example.invalid>"));
            Assert.That(root.GetProperty("to").EnumerateArray().Select(e => e.GetString()),
                Is.EqualTo(new[] { "thomashymas@gmail.com", "second@example.invalid" }));
            Assert.That(root.GetProperty("subject").GetString(), Is.EqualTo(Message.Subject));
            Assert.That(root.GetProperty("html").GetString(), Is.EqualTo(Message.Html));
            Assert.That(root.GetProperty("text").GetString(), Is.EqualTo(Message.Text));
        });
    }

    [Test]
    public async Task Each_send_carries_a_fresh_well_formed_idempotency_key()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"49a3999c-0ce1-4ea6-ab68-afcd6dc2e794"}""");
        var sender = BuildSender(handler);

        await sender.SendAsync(Message, CancellationToken.None);
        var first = handler.LastRequest!.Headers.GetValues("Idempotency-Key").Single();
        await sender.SendAsync(Message, CancellationToken.None);
        var second = handler.LastRequest!.Headers.GetValues("Idempotency-Key").Single();

        Assert.Multiple(() =>
        {
            Assert.That(first, Does.Match("^dr-[0-9a-f]{32}$"));
            Assert.That(second, Does.Match("^dr-[0-9a-f]{32}$"));
            Assert.That(first, Is.Not.EqualTo(second), "a forced resend must not collide with the morning's key");
        });
    }

    [Test]
    public async Task Success_response_becomes_a_receipt_with_the_returned_id()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"49a3999c-0ce1-4ea6-ab68-afcd6dc2e794"}""");
        var sender = BuildSender(handler);

        var receipt = await sender.SendAsync(Message, CancellationToken.None);

        Assert.That(receipt.ProviderMessageId, Is.EqualTo("49a3999c-0ce1-4ea6-ab68-afcd6dc2e794"));
    }

    [Test]
    public void Validation_error_response_throws_EmailSendException_with_status_and_message()
    {
        var handler = new RecordingHttpMessageHandler(
            (HttpStatusCode)422,
            """{"statusCode":422,"name":"validation_error","message":"to must be an array of email addresses"}""");
        var sender = BuildSender(handler);

        var exception = Assert.ThrowsAsync<EmailSendException>(async () => await sender.SendAsync(Message, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.StatusCode, Is.EqualTo(422));
            Assert.That(exception.BodyExcerpt, Does.Contain("to must be an array of email addresses"));
        });
    }

    [Test]
    public void Empty_api_key_throws_MissingSecretException_before_any_http_call()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"id":"unused"}""");
        var sender = BuildSender(handler, apiKey: "");

        var exception = Assert.ThrowsAsync<MissingSecretException>(async () => await sender.SendAsync(Message, CancellationToken.None));

        Assert.That(exception!.Name, Is.EqualTo("RESEND_API_KEY"));
        Assert.That(handler.CallCount, Is.EqualTo(0));
    }
}
