namespace DailyReport.Tests.Render;

/// <summary>
/// Byte-for-byte comparison against a file under <c>tests/DailyReport.Tests/Golden</c>. With
/// <c>UPDATE_GOLDEN=1</c> set, (re)writes the golden file into the source tree instead of asserting, so a
/// human can regenerate and then eyeball it. The csproj copies <c>Golden\**\*</c> to the output directory,
/// which is what a normal run compares against; a golden update walks back up to the source directory so the
/// file lands where source control sees it.
/// </summary>
internal static class GoldenFile
{
    public static void AssertMatches(string actual, string fileName)
    {
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            var sourcePath = Path.Combine(FindProjectDirectory(), "Golden", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, actual);
            Assert.Pass($"Golden file written: {sourcePath}");
            return;
        }

        var outputPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Golden", fileName);
        Assert.That(File.Exists(outputPath), Is.True, $"Golden file not found: {outputPath}. Run with UPDATE_GOLDEN=1 to create it.");
        var expected = File.ReadAllText(outputPath);
        Assert.That(actual, Is.EqualTo(expected));
    }

    /// <summary>Walks up from the test run's output directory to the project folder that holds <c>Golden/</c> in source control.</summary>
    private static string FindProjectDirectory()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DailyReport.Tests.csproj")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not find DailyReport.Tests.csproj by walking up from " + TestContext.CurrentContext.TestDirectory);
        }

        return dir.FullName;
    }
}
