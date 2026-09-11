using DailyReport.Core.Model;

namespace DailyReport.Core.Render;

/// <summary>The email subject, built once so the worker and any --dry-run tooling agree with the renderers on wording.</summary>
public static class SubjectLine
{
    public static string For(Report report)
    {
        var date = Formatting.IsoDate(report.ReportDate);

        return report.Status switch
        {
            ReportStatus.Red => $"🔴 {report.RedCount} failed · Daily report {date}",
            ReportStatus.Amber => $"🟠 {report.WarnCount} warning{(report.WarnCount == 1 ? "" : "s")} · Daily report {date}",
            _ => $"Daily report {date} · all green",
        };
    }
}
