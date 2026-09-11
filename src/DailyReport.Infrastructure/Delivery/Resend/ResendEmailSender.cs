using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using DailyReport.Infrastructure.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DailyReport.Infrastructure.Delivery.Resend;

/// <summary>Sends the daily report through Resend's `POST /emails`. Every send carries an
/// <c>Idempotency-Key</c> derived from the subject and recipient list, so the standard resilience
/// handler's retries on 5xx/408/429 can never double-send the same day's report.</summary>
public sealed class ResendEmailSender : IEmailSender
{
    internal const string HttpClientName = "resend";

    private static readonly JsonSerializerOptions SerializerOptions = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(IHttpClientFactory httpClientFactory, IOptions<ResendOptions> options, ILogger<ResendEmailSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailReceipt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            throw new MissingSecretException("RESEND_API_KEY");
        }

        var body = new ResendSendRequest
        {
            From = $"{message.FromName} <{message.From}>",
            To = message.To,
            Subject = message.Subject,
            Html = message.Html,
            Text = message.Text,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/emails")
        {
            Content = JsonContent.Create(body, options: SerializerOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add("Idempotency-Key", CreateIdempotencyKey());

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new EmailSendException((int)response.StatusCode, Excerpt(responseBody));
        }

        var parsed = Deserialize(responseBody);
        if (parsed?.Id is not { Length: > 0 } id)
        {
            throw new EmailSendException((int)response.StatusCode, Excerpt(responseBody));
        }

        _logger.LogInformation(
            "Sent daily report {Subject} to {RecipientCount} recipients, id {Id}",
            message.Subject,
            message.To.Count,
            id);

        return new EmailReceipt(id);
    }

    /// <summary>
    /// A fresh key per send attempt. The resilience handler's retries inside one attempt reuse the same request,
    /// so a POST that timed out after landing is not duplicated; a deliberate resend (--force, or a catch-up after
    /// a crash between send and record) gets a new key and goes through. Resend keeps a key for 24 h and answers
    /// 409 when the payload differs, and every render differs (timestamps, probe timings), so a stable per-day key
    /// would make --force impossible for a day. Twice beats never.
    /// </summary>
    public static string CreateIdempotencyKey() => "dr-" + Guid.NewGuid().ToString("N");

    private static ResendSendResponse? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ResendSendResponse>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Excerpt(string body) => body.Length <= 500 ? body : body[..500];
}
