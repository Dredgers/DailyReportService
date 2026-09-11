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
        request.Headers.Add("Idempotency-Key", CreateIdempotencyKey(message.Subject, message.To));

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

    /// <summary>`dr-` plus the first 32 hex characters of SHA-256(subject + "\n" + to joined by commas). The
    /// subject carries the report date, so retrying the same day's send reuses the key while a different
    /// day's report gets a fresh one.</summary>
    public static string CreateIdempotencyKey(string subject, IReadOnlyList<string> to)
    {
        var input = subject + "\n" + string.Join(",", to);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "dr-" + Convert.ToHexStringLower(hash)[..32];
    }

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
