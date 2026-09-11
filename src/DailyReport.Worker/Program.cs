using DailyReport.Worker.Hosting;
using Microsoft.Extensions.Logging.Console;

// One binary, two modes.
//   default        : BackgroundService that runs the report at Report:SendAtLocalTime every day (R9).
//   --once         : run one report now and exit; the exit code says whether it was sent.
//   --dry-run      : with --once, render to stdout and StateDirectory instead of sending.
//   --date=YYYY-MM-DD : with --once, pretend today is that date (re-run a past report).
var invocation = Invocation.Parse(args);

var builder = Host.CreateApplicationBuilder(args);

// Flat secret names from the deploy runbook map onto configuration keys here, so appsettings stays secret-free.
builder.Configuration.AddSecretEnvironmentVariables();

builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment() || invocation.Once)
{
    builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
}
else
{
    // Docker keeps stdout; one JSON object per line is what a log shipper wants later.
    builder.Logging.AddJsonConsole(o => o.JsonWriterOptions = new() { Indented = false });
}

builder.Services.AddDailyReport(builder.Configuration);

if (!invocation.Once)
{
    builder.Services.AddHostedService<ReportSchedulerService>();
}

using var host = builder.Build();

if (invocation.Once)
{
    // Options validation runs on the first resolve; a bad config fails here, loudly, before any I/O.
    using var scope = host.Services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<ReportRunner>();
    return await runner.RunOnceAsync(invocation, CancellationToken.None);
}

await host.RunAsync();
return 0;
