using System.Globalization;
using System.Net;
using System.Text;
using DailyReport.Core.Model;

namespace DailyReport.Core.Render;

/// <summary>
/// Renders a <see cref="Report"/> as a complete, self-contained HTML email: inline styles on every element
/// (the small &lt;style&gt; block in &lt;head&gt; is progressive enhancement only, stripped by Gmail and some
/// clients), one column, max-width 640px, system font stack, table-based metric rows. Every dynamic string is
/// HTML-encoded. Deterministic: no wall-clock reads, invariant culture, "\n" line endings.
/// </summary>
public static class HtmlRenderer
{
    private const string RedBackground = "#7a1212";
    private const string AmberBackground = "#fff4e0";
    private const string AmberText = "#7a4a00";
    private const string MutedText = "#888888";
    private const string BodyText = "#222222";
    private const string HeadingText = "#111111";
    private const string RuleColor = "#eeeeee";
    private const string FontStack = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    public static string Render(Report report)
    {
        var names = RenderSupport.GameNames(report);
        var sb = new StringBuilder();
        void L(string s = "") => sb.Append(s).Append('\n');

        L("<!doctype html>");
        L("<html lang=\"en\">");
        L("<head>");
        L("<meta charset=\"utf-8\">");
        L("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        L($"<title>{Encode(SubjectLine.For(report))}</title>");
        L("<style>");
        L("  body { margin: 0; padding: 0; }");
        L("  table { border-collapse: collapse; }");
        L("</style>");
        L("</head>");
        L($"<body style=\"margin:0;padding:0;background-color:#f2f2f2;font-family:{FontStack};\">");
        L("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:#f2f2f2;\">");
        L("<tr><td align=\"center\" style=\"padding:24px 16px;\">");
        L($"<table role=\"presentation\" width=\"640\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;max-width:640px;background-color:#ffffff;font-family:{FontStack};\">");

        Header(sb, report);

        if (report.RedCount > 0)
        {
            RedBanner(sb, report, names);
        }

        if (report.WarnCount > 0)
        {
            AmberBlock(sb, report, names);
        }

        HealthSection(sb, report, names);

        foreach (var section in report.Games)
        {
            GameSectionBlock(sb, report, section);
        }

        Footer(sb, report);

        L("</table>");
        L("</td></tr>");
        L("</table>");
        L("</body>");
        L("</html>");

        return sb.ToString();
    }

    private static void Header(StringBuilder sb, Report report)
    {
        void L(string s = "") => sb.Append(s).Append('\n');
        var coveredDay = Formatting.DayName(report.ReportDate.AddDays(-1));
        var sent = Formatting.IsoDate(report.ReportDate);

        L("<tr><td style=\"padding:24px 24px 16px 24px;\">");
        L($"<div style=\"font-size:22px;line-height:28px;font-weight:700;color:{HeadingText};\">Daily report</div>");
        L($"<div style=\"font-size:16px;line-height:22px;color:#333333;margin-top:4px;\">{Encode(coveredDay)}</div>");
        L($"<div style=\"font-size:12px;line-height:16px;color:{MutedText};margin-top:8px;\">sent {Encode(sent)}</div>");
        L("</td></tr>");
    }

    private static void RedBanner(StringBuilder sb, Report report, IReadOnlyDictionary<string, string> names)
    {
        void L(string s = "") => sb.Append(s).Append('\n');

        L("<tr><td style=\"padding:0 24px 16px 24px;\">");
        L($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:{RedBackground};border-radius:6px;\">");
        L("<tr><td style=\"padding:16px 20px;\">");
        L($"<div style=\"font-size:16px;font-weight:700;color:#ffffff;margin-bottom:8px;\">{report.RedCount} {RenderSupport.Pluralise(report.RedCount, "thing needs attention", "things need attention")}</div>");
        L("<ul style=\"margin:0;padding-left:20px;color:#ffffff;font-size:13px;line-height:19px;\">");

        foreach (var c in report.Health.Where(c => c.IsRed))
        {
            L($"<li style=\"margin-bottom:4px;\">{Encode(RenderSupport.GameName(names, c.GameKey))} — {Encode(c.Name)}: {Encode(c.Detail)}</li>");
        }

        foreach (var f in report.Failures)
        {
            L($"<li style=\"margin-bottom:4px;\">{Encode(RenderSupport.GameName(names, f.GameKey))} — {Encode(f.Section)}: {Encode(f.Summary)}</li>");
        }

        L("</ul>");
        L("</td></tr>");
        L("</table>");
        L("</td></tr>");
    }

    private static void AmberBlock(StringBuilder sb, Report report, IReadOnlyDictionary<string, string> names)
    {
        void L(string s = "") => sb.Append(s).Append('\n');
        var title = RenderSupport.Pluralise(report.WarnCount, "warning", "warnings");

        L("<tr><td style=\"padding:0 24px 16px 24px;\">");
        L($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:{AmberBackground};border-radius:6px;\">");
        L("<tr><td style=\"padding:16px 20px;\">");
        L($"<div style=\"font-size:16px;font-weight:700;color:{AmberText};margin-bottom:8px;\">{report.WarnCount} {title} to review</div>");
        L($"<ul style=\"margin:0;padding-left:20px;color:{AmberText};font-size:13px;line-height:19px;\">");

        foreach (var c in report.Health.Where(c => c.Status == CheckStatus.Warn))
        {
            L($"<li style=\"margin-bottom:4px;\">{Encode(RenderSupport.GameName(names, c.GameKey))} — {Encode(c.Name)}: {Encode(c.Detail)}</li>");
        }

        L("</ul>");
        L("</td></tr>");
        L("</table>");
        L("</td></tr>");
    }

    private static void HealthSection(StringBuilder sb, Report report, IReadOnlyDictionary<string, string> names)
    {
        void L(string s = "") => sb.Append(s).Append('\n');

        L("<tr><td style=\"padding:8px 24px 16px 24px;\">");
        L($"<div style=\"font-size:18px;font-weight:700;color:{HeadingText};margin-bottom:8px;\">Health</div>");
        L($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:13px;line-height:18px;color:{BodyText};\">");
        L("<tr>");
        foreach (var head in new[] { "Game", "Check", "Status", "Detail", "Elapsed" })
        {
            L($"<th align=\"left\" style=\"padding:6px 8px;border-bottom:1px solid #dddddd;color:#666666;font-size:11px;text-transform:uppercase;\">{head}</th>");
        }

        L("</tr>");

        foreach (var c in report.Health)
        {
            L("<tr>");
            L($"<td style=\"padding:6px 8px;border-bottom:1px solid {RuleColor};\">{Encode(RenderSupport.GameName(names, c.GameKey))}</td>");
            L($"<td style=\"padding:6px 8px;border-bottom:1px solid {RuleColor};\">{Encode(c.Name)}</td>");
            L($"<td style=\"padding:6px 8px;border-bottom:1px solid {RuleColor};white-space:nowrap;\">{Encode(RenderSupport.StatusPill(c.Status))}</td>");
            L($"<td style=\"padding:6px 8px;border-bottom:1px solid {RuleColor};\">{Encode(c.Detail)}</td>");
            L($"<td style=\"padding:6px 8px;border-bottom:1px solid {RuleColor};text-align:right;white-space:nowrap;\">{Encode(RenderSupport.ElapsedMs(c.Elapsed))}</td>");
            L("</tr>");
        }

        L("</table>");
        L("</td></tr>");
    }

    private static void GameSectionBlock(StringBuilder sb, Report report, GameSection section)
    {
        void L(string s = "") => sb.Append(s).Append('\n');

        L("<tr><td style=\"padding:16px 24px;border-top:1px solid #dddddd;\">");
        L($"<div style=\"font-size:18px;font-weight:700;color:{HeadingText};\">{Encode(section.Name)}</div>");
        L($"<div style=\"font-size:12px;color:{MutedText};margin-bottom:12px;\">{Encode(Formatting.DayName(section.GameDay))} · {Encode(section.ZoneId)}</div>");

        if (section.Metrics.Count == 0)
        {
            var failures = RenderSupport.FailuresFor(report, section.GameKey);
            var summary = failures.Count > 0 ? string.Join("; ", failures.Select(f => f.Summary)) : "no data available.";
            L($"<div style=\"color:{RedBackground};font-size:13px;font-weight:600;\">Metrics unavailable: {Encode(summary)}</div>");
        }
        else
        {
            foreach (var groupName in GroupsInOrder(section.Metrics))
            {
                L($"<div style=\"font-size:12px;font-weight:700;color:#666666;text-transform:uppercase;letter-spacing:0.03em;margin:12px 0 4px 0;\">{Encode(groupName.Key)}</div>");
                L($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:13px;line-height:17px;color:{BodyText};margin-bottom:4px;\">");

                foreach (var m in groupName.Value)
                {
                    MetricRow(sb, m);
                }

                L("</table>");
            }
        }

        L("</td></tr>");
    }

    private static void MetricRow(StringBuilder sb, Metric m)
    {
        void L(string s = "") => sb.Append(s).Append('\n');

        var laneTag = m.Definition.Lane == Lane.SignedIn
            ? $" <span style=\"color:{MutedText};font-size:11px;\">signed-in</span>"
            : string.Empty;
        var note = m.Definition.Note is null
            ? string.Empty
            : $"<div style=\"color:{MutedText};font-size:11px;\">{Encode(m.Definition.Note)}</div>";
        var baselineNote = Formatting.BaselineNote(m.BaselineDaysPresent);
        var baselineTag = baselineNote is null
            ? string.Empty
            : $" <span style=\"color:#aaaaaa;\">({Encode(baselineNote)})</span>";

        L("<tr>");
        L($"<td style=\"padding:6px 8px 6px 0;vertical-align:top;border-bottom:1px solid {RuleColor};\">{Encode(m.Definition.Label)}{laneTag}{note}</td>");
        L($"<td style=\"padding:6px 8px;vertical-align:top;text-align:right;font-weight:600;white-space:nowrap;border-bottom:1px solid {RuleColor};\">{Encode(Formatting.Value(m.Value, m.Definition.Unit))}</td>");
        L($"<td style=\"padding:6px 8px;vertical-align:top;border-bottom:1px solid {RuleColor};\"><div style=\"color:{MutedText};font-size:11px;\">vs day before</div><div style=\"white-space:nowrap;\">{Encode(Formatting.Delta(m.VsDayBefore, m.Definition.Unit))}</div></td>");
        L($"<td style=\"padding:6px 8px;vertical-align:top;border-bottom:1px solid {RuleColor};\"><div style=\"color:{MutedText};font-size:11px;\">vs 7-day mean{baselineTag}</div><div style=\"white-space:nowrap;\">{Encode(Formatting.Delta(m.VsSevenDayMean, m.Definition.Unit))}</div></td>");
        L($"<td style=\"padding:6px 8px;vertical-align:top;border-bottom:1px solid {RuleColor};\"><div style=\"color:{MutedText};font-size:11px;\">vs same weekday</div><div style=\"white-space:nowrap;\">{Encode(Formatting.Delta(m.VsSameWeekdayLastWeek, m.Definition.Unit))}</div></td>");
        L("</tr>");
    }

    private static void Footer(StringBuilder sb, Report report)
    {
        void L(string s = "") => sb.Append(s).Append('\n');
        var zones = string.Join(", ", report.Games.Select(g => $"{g.Name} in {g.ZoneId}"));
        var generated = report.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        L("<tr><td style=\"padding:16px 24px 24px 24px;border-top:1px solid #dddddd;\">");
        L($"<div style=\"font-size:11px;line-height:16px;color:#999999;\">Days are counted in each game's own zone: {Encode(zones)}.</div>");
        L($"<div style=\"font-size:11px;line-height:16px;color:#999999;margin-top:4px;\">Generated {Encode(generated)} UTC</div>");
        L("</td></tr>");
    }

    /// <summary>Metrics grouped by <see cref="MetricDefinition.Group"/>, preserving the order each group first appears in.</summary>
    private static List<KeyValuePair<string, List<Metric>>> GroupsInOrder(IReadOnlyList<Metric> metrics)
    {
        var order = new List<string>();
        var byGroup = new Dictionary<string, List<Metric>>();

        foreach (var m in metrics)
        {
            if (!byGroup.TryGetValue(m.Definition.Group, out var list))
            {
                list = [];
                byGroup[m.Definition.Group] = list;
                order.Add(m.Definition.Group);
            }

            list.Add(m);
        }

        return order.Select(g => new KeyValuePair<string, List<Metric>>(g, byGroup[g])).ToList();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
