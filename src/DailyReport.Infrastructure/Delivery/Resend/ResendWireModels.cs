using System.Text.Json.Serialization;

namespace DailyReport.Infrastructure.Delivery.Resend;

/// <summary>The `POST /emails` request body. Property names are already lowercase on the wire, so each one
/// carries an explicit <see cref="JsonPropertyNameAttribute"/> rather than relying on a naming policy.</summary>
internal sealed class ResendSendRequest
{
    [JsonPropertyName("from")]
    public required string From { get; init; }

    [JsonPropertyName("to")]
    public required IReadOnlyList<string> To { get; init; }

    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    [JsonPropertyName("html")]
    public required string Html { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>The success body: <c>{ "id": "..." }</c>.</summary>
internal sealed class ResendSendResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}
