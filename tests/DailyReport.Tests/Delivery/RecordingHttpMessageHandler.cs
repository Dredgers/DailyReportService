using System.Net;
using System.Text;

namespace DailyReport.Tests.Delivery;

/// <summary>Captures the single request it receives and returns a canned response. No network, no
/// <see cref="System.Net.Http.HttpClient"/> BaseAddress needed since <see cref="ResendEmailSender"/> always
/// sends absolute request URIs.</summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>An <see cref="IHttpClientFactory"/> of one client, backed by whatever handler the test wired up.</summary>
internal sealed class SingleClientHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
