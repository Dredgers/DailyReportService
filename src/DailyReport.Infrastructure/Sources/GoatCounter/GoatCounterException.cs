using System.Net;

namespace DailyReport.Infrastructure.Sources.GoatCounter;

/// <summary>
/// Any failure talking to GoatCounter: a non-2xx response, or a 2xx response whose body does not match the
/// shape <see cref="GoatCounterClient"/> expects. Never thrown for a missing token; that is
/// <see cref="Secrets.MissingSecretException"/>, so a misconfigured secret is distinguishable from a dead API.
/// </summary>
public sealed class GoatCounterException : Exception
{
    public GoatCounterException(HttpStatusCode statusCode, Uri requestUri, string bodyExcerpt)
        : base(FormatMessage(statusCode, requestUri, bodyExcerpt))
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
        BodyExcerpt = bodyExcerpt;
    }

    public GoatCounterException(HttpStatusCode statusCode, Uri requestUri, string bodyExcerpt, Exception innerException)
        : base(FormatMessage(statusCode, requestUri, bodyExcerpt), innerException)
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
        BodyExcerpt = bodyExcerpt;
    }

    public HttpStatusCode StatusCode { get; }

    public Uri RequestUri { get; }

    /// <summary>The response body, truncated to at most 500 characters. Never the request headers, so never the token.</summary>
    public string BodyExcerpt { get; }

    private static string FormatMessage(HttpStatusCode statusCode, Uri requestUri, string bodyExcerpt) =>
        $"GoatCounter request to {requestUri} returned {(int)statusCode} {statusCode}: {bodyExcerpt}";
}
