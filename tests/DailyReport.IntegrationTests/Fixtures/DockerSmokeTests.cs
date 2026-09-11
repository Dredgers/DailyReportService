using Npgsql;
using Testcontainers.PostgreSql;

namespace DailyReport.IntegrationTests.Fixtures;

/// <summary>Proves the machine can run Testcontainers at all. R4 replaces this with the real schema fixture.</summary>
public sealed class DockerSmokeTests
{
    [Test]
    public async Task Postgres_container_answers()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select 1", connection);

        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(1));
    }
}
