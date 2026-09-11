namespace DailyReport.Infrastructure.Delivery.Resend;

/// <summary>Thrown when Resend responds with a non-2xx status. <see cref="BodyExcerpt"/> is capped at 500
/// characters; callers must not log it alongside secrets, but it carries no key material of its own.</summary>
public sealed class EmailSendException(int statusCode, string bodyExcerpt)
    : Exception($"Resend returned {statusCode}: {bodyExcerpt}")
{
    public int StatusCode { get; } = statusCode;

    public string BodyExcerpt { get; } = bodyExcerpt;
}
