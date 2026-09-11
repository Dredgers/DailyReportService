namespace DailyReport.Tests.Fixtures;

/// <summary>
/// The copies under sql/fixtures must match the sibling game repos byte for byte, otherwise the integration
/// tests are proving a schema that no longer exists. Runs only where the siblings are checked out; skips elsewhere.
/// </summary>
public sealed class FixtureDriftTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly (string Game, string RelativeSibling)[] Siblings =
    [
        ("crosswords", Path.Combine("..", "..", "Competitive Crosswords", "CompetitiveCrosswords", "server", "sql")),
        ("makemered", Path.Combine("..", "..", "Make It Red", "server", "sql")),
    ];

    [TestCaseSource(nameof(Siblings))]
    public void Copies_match_the_sibling_repo((string Game, string RelativeSibling) sibling)
    {
        var source = Environment.GetEnvironmentVariable(sibling.Game == "crosswords" ? "CC_SQL_DIR" : "MMR_SQL_DIR")
                     ?? Path.GetFullPath(Path.Combine(RepoRoot, sibling.RelativeSibling));
        if (!Directory.Exists(source))
        {
            Assert.Ignore($"Sibling repo not present at {source}; run ops/sync-fixtures.sh where it is.");
        }

        var copies = Path.Combine(RepoRoot, "sql", "fixtures", sibling.Game);
        var drift = new List<string>();

        foreach (var file in Directory.EnumerateFiles(source).Where(f => f.EndsWith(".sql", StringComparison.Ordinal) || Path.GetFileName(f) == "README.md"))
        {
            var copy = Path.Combine(copies, Path.GetFileName(file));
            if (!File.Exists(copy) || !File.ReadAllBytes(copy).AsSpan().SequenceEqual(File.ReadAllBytes(file)))
            {
                drift.Add(Path.GetFileName(file));
            }
        }

        Assert.That(drift, Is.Empty, $"sql/fixtures/{sibling.Game} has drifted; run ops/sync-fixtures.sh and review the ORDER file.");
    }

    [Test]
    public void Order_files_only_name_files_that_exist()
    {
        foreach (var (game, _) in Siblings)
        {
            var dir = Path.Combine(RepoRoot, "sql", "fixtures", game);
            var order = File.ReadAllLines(Path.Combine(dir, "ORDER")).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'));
            foreach (var file in order)
            {
                Assert.That(File.Exists(Path.Combine(dir, file)), Is.True, $"{game}/ORDER names missing file {file}");
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DailyReportService.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repo root from the test directory.");
    }
}
