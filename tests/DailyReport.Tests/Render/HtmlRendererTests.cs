using DailyReport.Core.Model;
using DailyReport.Core.Render;

namespace DailyReport.Tests.Render;

public sealed class HtmlRendererTests
{
    [Test]
    public void A_metric_label_containing_a_script_tag_is_html_encoded_not_injected()
    {
        var definition = new MetricDefinition("evil", "Traffic", "<script>alert(1)</script>", Lane.All, MetricUnit.Count);
        var metric = new Metric(definition, Value: 1, DayBefore: null, SevenDayMean: null, BaselineDaysPresent: 0, SameWeekdayLastWeek: null);
        var section = new GameSection("g", "G", new DateOnly(2026, 9, 10), "UTC", [metric]);
        var report = new Report(
            new DateOnly(2026, 9, 11),
            new DateTimeOffset(2026, 9, 11, 5, 0, 0, TimeSpan.Zero),
            [CheckResult.Pass("g", "Healthz", "200 OK", TimeSpan.Zero)],
            [section],
            []);

        var html = HtmlRenderer.Render(report);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("<script>"));
            Assert.That(html, Does.Contain("&lt;script&gt;"));
        });
    }

    [Test]
    public void The_red_banner_is_absent_when_the_report_is_all_green()
    {
        var html = HtmlRenderer.Render(ReportFixtures.AllGreen());

        Assert.That(html, Does.Not.Contain("things need attention"));
    }
}
