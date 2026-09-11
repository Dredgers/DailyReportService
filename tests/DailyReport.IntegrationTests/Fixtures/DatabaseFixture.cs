using Testcontainers.PostgreSql;

namespace DailyReport.IntegrationTests;

/// <summary>
/// One Postgres container for the whole assembly, built from the games' real SQL: the auth shim, then every
/// crosswords file in its ORDER, then every Make Me Red file in its ORDER, then report_ro.sql. Scripts run
/// through psql inside the container with ON_ERROR_STOP, the closest thing to the dashboard's SQL editor.
/// </summary>
[SetUpFixture]
public sealed class DatabaseFixture
{
    public const string ReportRoPassword = "report_ro_test_password";

    private static PostgreSqlContainer? container;

    public static string AdminConnectionString { get; private set; } = "";

    public static string ReportRoConnectionString { get; private set; } = "";

    [OneTimeSetUp]
    public async Task StartAndApplySchemaAsync()
    {
        container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();

        foreach (var script in SqlScripts.InApplyOrder())
        {
            await ApplyAsync(script);
        }

        AdminConnectionString = container.GetConnectionString();
        ReportRoConnectionString = SqlScripts.WithCredentials(AdminConnectionString, "report_ro", ReportRoPassword);
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    private static async Task ApplyAsync(SqlScript script)
    {
        var body = script.Content;
        if (script.Path.EndsWith("report_ro.sql", StringComparison.Ordinal))
        {
            body = body.Replace("CHANGE_ME_STRONG_PASSWORD", ReportRoPassword, StringComparison.Ordinal);
        }

        var result = await container!.ExecScriptAsync("\\set ON_ERROR_STOP on\n" + body);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{script.Label} failed (exit {result.ExitCode}):\n{result.Stderr}\n{result.Stdout}");
        }
    }
}
