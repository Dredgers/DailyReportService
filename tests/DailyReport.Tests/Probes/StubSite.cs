using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DailyReport.Tests.Probes;

/// <summary>
/// An in-process Kestrel server for probe tests. Register handlers with <see cref="Map"/>, call
/// <see cref="StartAsync"/>, then point a probe's <c>GameOptions.BaseUrl</c> (or a
/// <c>PuzzleSourceOptions.Url</c>) at <see cref="BaseUri"/>. Disposing stops the server.
/// </summary>
public sealed class StubSite : IAsyncDisposable
{
    private readonly WebApplication _app;
    private bool _started;

    public StubSite()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();
    }

    /// <summary>The bound origin, only valid after <see cref="StartAsync"/> returns.</summary>
    public string BaseUri { get; private set; } = "";

    /// <summary>Registers a GET handler for <paramref name="path"/>. Call before <see cref="StartAsync"/>.</summary>
    public StubSite Map(string path, Func<HttpContext, Task> handler)
    {
        _app.MapGet(path, handler);
        return this;
    }

    public async Task StartAsync()
    {
        await _app.StartAsync().ConfigureAwait(false);
        BaseUri = _app.Urls.First().TrimEnd('/');
        _started = true;
    }

    /// <summary>
    /// A loopback address nothing is listening on, for "connection refused" tests: binds a socket long enough
    /// to learn a free port from the OS, then releases it without ever answering a request on it.
    /// </summary>
    public static string GetClosedPortUri()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await _app.StopAsync().ConfigureAwait(false);
        }

        await _app.DisposeAsync().ConfigureAwait(false);
    }
}
