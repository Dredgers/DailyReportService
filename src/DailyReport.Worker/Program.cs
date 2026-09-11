using DailyReport.Worker.Hosting;

// One binary, three modes.
//   default            : BackgroundService that runs the report at Report:SendAtLocalTime every day.
//   --once             : run one report now and exit; exit code 1 only if the send failed.
//       --dry-run      : render to stdout and the archive instead of sending.
//       --date=YYYY-MM-DD : pretend today is that date (re-run a past report).
//       --force        : send even if that date was already sent.
//   --healthcheck      : exit 0 while the last successful run is recent (Docker HEALTHCHECK).
var invocation = Invocation.Parse(args);

var builder = Host.CreateApplicationBuilder(args);

// Flat secret names from the deploy runbook map onto configuration keys here, so appsettings stays secret-free.
builder.Configuration.AddSecretEnvironmentVariables();

builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment() || invocation.Once || invocation.HealthCheck)
{
    builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
}
else
{
    // Docker keeps stdout; one JSON object per line is what a log shipper wants later.
    builder.Logging.AddJsonConsole(o => o.JsonWriterOptions = new() { Indented = false });
}

builder.Services.AddDailyReport(builder.Configuration);

if (!invocation.Once && !invocation.HealthCheck)
{
    builder.Services.AddHostedService<ReportSchedulerService>();
}

using var host = builder.Build();

if (invocation.HealthCheck)
{
    return await HealthCheck.RunAsync(host.Services, CancellationToken.None);
}

if (invocation.Once)
{
    // Options validation runs on the first resolve; a bad config fails here, loudly, before any I/O.
    using var scope = host.Services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<ReportRunner>();
    return await runner.RunOnceAsync(invocation, CancellationToken.None);
}

await host.RunAsync();
return 0;
