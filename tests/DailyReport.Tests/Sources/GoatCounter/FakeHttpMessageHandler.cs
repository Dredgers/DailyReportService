using System.Net;
using System.Text;

namespace DailyReport.Tests.Sources.GoatCounter;

/// <summary>Records every request sent through it and answers with canned responses, in the order queued. No network.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public void Enqueue(HttpStatusCode statusCode, string body, string mediaType = "application/json") =>
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory) => _responses.Enqueue(factory);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"FakeHttpMessageHandler: no canned response queued for {request.RequestUri}.");
        }

        return Task.FromResult(_responses.Dequeue().Invoke(request));
    }
}

/// <summary>Hands out HttpClients backed by one <see cref="FakeHttpMessageHandler"/>, standing in for IHttpClientFactory.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
