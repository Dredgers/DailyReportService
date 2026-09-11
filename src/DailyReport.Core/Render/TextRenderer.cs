using System.Globalization;
using System.Text;
using DailyReport.Core.Model;

namespace DailyReport.Core.Render;

/// <summary>
/// Renders a <see cref="Report"/> as the plain-text alternative part: same content and order as
/// <see cref="HtmlRenderer"/>, ~76 columns, aligned metric columns, "="/"-" underlines for headings, one blank
/// line between blocks. Deterministic: no wall-clock reads, invariant culture, "\n" line endings.
/// </summary>
public static class TextRenderer
{
    private const int Width = 76;
    private const int SubLabelWidth = 17;

    public static string Render(Report report)
    {
        var names = RenderSupport.GameNames(report);
        var blocks = new List<string>
        {
            HeaderBlock(report),
        };

        if (report.RedCount > 0)
        {
            blocks.Add(RedBannerBlock(report, names));
        }

        if (report.WarnCount > 0)
        {
            blocks.Add(AmberBlock(report, names));
        }

        blocks.Add(HealthBlock(report, names));

        foreach (var section in report.Games)
        {
            blocks.Add(GameSectionBlock(report, section));
        }

        blocks.Add(FooterBlock(report));

        return string.Join("\n\n", blocks) + "\n";
    }

    private static string HeaderBlock(Report report)
    {
        var coveredDay = Formatting.DayName(report.ReportDate.AddDays(-1));
        var sent = Formatting.IsoDate(report.ReportDate);

        var sb = new StringBuilder();
        sb.Append("Daily report").Append('\n');
        sb.Append(coveredDay).Append('\n');
        sb.Append("sent ").Append(sent).Append('\n');
        sb.Append(Rule('='));
        return sb.ToString();
    }

    private static string RedBannerBlock(Report report, IReadOnlyDictionary<string, string> names)
    {
        var sb = new StringBuilder();
        sb.Append(RenderSupport.Pluralise(report.RedCount, "THING NEEDS ATTENTION", "THINGS NEED ATTENTION")).Append('\n');
        sb.Append(Rule('-'));

        foreach (var c in report.Health.Where(c => c.IsRed))
        {
            sb.Append('\n').Append("- ").Append(RenderSupport.GameName(names, c.GameKey)).Append(" — ").Append(c.Name).Append(": ").Append(c.Detail);
        }

        foreach (var f in report.Failures)
        {
            sb.Append('\n').Append("- ").Append(RenderSupport.GameName(names, f.GameKey)).Append(" — ").Append(f.Section).Append(": ").Append(f.Summary);
        }

        return sb.ToString();
    }

    private static string AmberBlock(Report report, IReadOnlyDictionary<string, string> names)
    {
        var title = RenderSupport.Pluralise(report.WarnCount, "WARNING", "WARNINGS");
        var sb = new StringBuilder();
        sb.Append(report.WarnCount).Append(' ').Append(title).Append('\n');
        sb.Append(Rule('-'));

        foreach (var c in report.Health.Where(c => c.Status == CheckStatus.Warn))
        {
            sb.Append('\n').Append("- ").Append(RenderSupport.GameName(names, c.GameKey)).Append(" — ").Append(c.Name).Append(": ").Append(c.Detail);
        }

        return sb.ToString();
    }

    private static string HealthBlock(Report report, IReadOnlyDictionary<string, string> names)
    {
        var sb = new StringBuilder();
        sb.Append("HEALTH").Append('\n');
        sb.Append(Rule('-'));

        foreach (var c in report.Health)
        {
            sb.Append('\n')
              .Append(RenderSupport.StatusPill(c.Status)).Append("  ")
              .Append(RenderSupport.GameName(names, c.GameKey)).Append(" — ")
              .Append(c.Name).Append(": ").Append(c.Detail)
              .Append(" (").Append(RenderSupport.ElapsedMs(c.Elapsed)).Append(')');
        }

        return sb.ToString();
    }

    private static string GameSectionBlock(Report report, GameSection section)
    {
        var sb = new StringBuilder();
        sb.Append(section.Name.ToUpperInvariant()).Append('\n');
        sb.Append(Formatting.DayName(section.GameDay)).Append(" · ").Append(section.ZoneId).Append('\n');
        sb.Append(Rule('-'));

        if (section.Metrics.Count == 0)
        {
            var failures = RenderSupport.FailuresFor(report, section.GameKey);
            var summary = failures.Count > 0 ? string.Join("; ", failures.Select(f => f.Summary)) : "no data available.";
            sb.Append('\n').Append("🔴 Metrics unavailable: ").Append(summary);
            return sb.ToString();
        }

        foreach (var groupName in GroupsInOrder(section.Metrics))
        {
            sb.Append('\n').Append('\n').Append(groupName.Key);

            foreach (var m in groupName.Value)
            {
                AppendMetric(sb, m);
            }
        }

        return sb.ToString();
    }

    private static void AppendMetric(StringBuilder sb, Metric m)
    {
        var laneTag = m.Definition.Lane == Lane.SignedIn ? " (signed-in)" : string.Empty;
        sb.Append('\n').Append("  ").Append(m.Definition.Label).Append(laneTag);

        if (m.Definition.Note is not null)
        {
            sb.Append('\n').Append("    ").Append(m.Definition.Note);
        }

        var baselineNote = Formatting.BaselineNote(m.BaselineDaysPresent);
        var baselineTag = baselineNote is null ? string.Empty : $"  ({baselineNote})";

        AppendSubLine(sb, "value", Formatting.Value(m.Value, m.Definition.Unit));
        AppendSubLine(sb, "vs day before", Formatting.Delta(m.VsDayBefore, m.Definition.Unit));
        AppendSubLine(sb, "vs 7-day mean", Formatting.Delta(m.VsSevenDayMean, m.Definition.Unit) + baselineTag);
        AppendSubLine(sb, "vs same weekday", Formatting.Delta(m.VsSameWeekdayLastWeek, m.Definition.Unit));
    }

    private static void AppendSubLine(StringBuilder sb, string label, string value) =>
        sb.Append('\n').Append("      ").Append(label.PadRight(SubLabelWidth)).Append(value);

    private static string FooterBlock(Report report)
    {
        var zones = string.Join(", ", report.Games.Select(g => $"{g.Name} in {g.ZoneId}"));
        var generated = report.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append(Rule('-')).Append('\n');
        sb.Append("Days are counted in each game's own zone: ").Append(zones).Append('.').Append('\n');
        sb.Append("Generated ").Append(generated).Append(" UTC");
        return sb.ToString();
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

    private static string Rule(char c) => new string(c, Width);
}
