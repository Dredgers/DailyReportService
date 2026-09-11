using DailyReport.Core.Render;

namespace DailyReport.Tests.Render;

public sealed class TextRendererGoldenTests
{
    [Test]
    public void AllGreen_matches_golden() =>
        GoldenFile.AssertMatches(TextRenderer.Render(ReportFixtures.AllGreen()), "AllGreen.txt");

    [Test]
    public void OneFailedOneWarn_matches_golden() =>
        GoldenFile.AssertMatches(TextRenderer.Render(ReportFixtures.OneFailedOneWarn()), "OneFailedOneWarn.txt");

    [Test]
    public void SourceFailure_matches_golden() =>
        GoldenFile.AssertMatches(TextRenderer.Render(ReportFixtures.SourceFailure()), "SourceFailure.txt");

    [Test]
    public void NoData_matches_golden() =>
        GoldenFile.AssertMatches(TextRenderer.Render(ReportFixtures.NoData()), "NoData.txt");
}
