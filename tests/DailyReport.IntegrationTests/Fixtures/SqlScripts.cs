using Npgsql;

namespace DailyReport.IntegrationTests;

public sealed record SqlScript(string Label, string Path, string Content);

/// <summary>Finds the linked copies of sql/ in the test output and reads the ORDER files.</summary>
public static class SqlScripts
{
    public static string SqlRoot => System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "sql");

    public static IEnumerable<SqlScript> InApplyOrder()
    {
        yield return Read("shim", System.IO.Path.Combine(SqlRoot, "fixtures", "auth_shim.sql"));

        foreach (var game in new[] { "crosswords", "makemered" })
        {
            var dir = System.IO.Path.Combine(SqlRoot, "fixtures", game);
            foreach (var file in Order(dir))
            {
                yield return Read($"{game}/{file}", System.IO.Path.Combine(dir, file));
            }
        }

        yield return Read("report_ro", System.IO.Path.Combine(SqlRoot, "report_ro.sql"));
    }

    public static IReadOnlyList<string> Order(string directory) =>
        File.ReadAllLines(System.IO.Path.Combine(directory, "ORDER"))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

    public static string WithCredentials(string connectionString, string username, string password)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Username = username, Password = password };
        return builder.ConnectionString;
    }

    private static SqlScript Read(string label, string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"SQL script for {label} not found. Did ops/sync-fixtures.sh run and is the file linked in the csproj?", path);
        }

        return new SqlScript(label, path, File.ReadAllText(path));
    }
}
