using DailyReport.Core.Render;

namespace DailyReport.Tests.Render;

public sealed class HtmlRendererGoldenTests
{
    [Test]
    public void AllGreen_matches_golden() =>
        GoldenFile.AssertMatches(HtmlRenderer.Render(ReportFixtures.AllGreen()), "AllGreen.html");

    [Test]
    public void OneFailedOneWarn_matches_golden() =>
        GoldenFile.AssertMatches(HtmlRenderer.Render(ReportFixtures.OneFailedOneWarn()), "OneFailedOneWarn.html");

    [Test]
    public void SourceFailure_matches_golden() =>
        GoldenFile.AssertMatches(HtmlRenderer.Render(ReportFixtures.SourceFailure()), "SourceFailure.html");

    [Test]
    public void NoData_matches_golden() =>
        GoldenFile.AssertMatches(HtmlRenderer.Render(ReportFixtures.NoData()), "NoData.html");
}
