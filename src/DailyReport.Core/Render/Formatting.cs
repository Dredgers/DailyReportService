using System.Globalization;
using DailyReport.Core.Model;

namespace DailyReport.Core.Render;

/// <summary>
/// Shared, deterministic, invariant-culture formatting for the HTML and text renderers. Nothing here reads
/// the clock or the current culture: a value renders the same way regardless of when or where it runs.
/// </summary>
public static class Formatting
{
    /// <summary>An en dash: the "no comparison available" delta.</summary>
    private const string NoDelta = "–";

    /// <summary>A count: whole number, thousands separators, invariant. Null is "no data", never zero.</summary>
    public static string Count(double? value) =>
        value is null ? "no data" : Round(value.Value).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A percentage: one decimal place plus "%". Null is "no data", never zero.</summary>
    public static string Percent(double? value) =>
        value is null ? "no data" : value.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    /// <summary>A metric value, formatted per its unit.</summary>
    public static string Value(double? value, MetricUnit unit) =>
        unit == MetricUnit.Percent ? Percent(value) : Count(value);

    /// <summary>
    /// A delta against a baseline: "&#9650; 12 (+9.4%)", "&#9660; 3 (&#8722;2.1%)", "= 0" when unchanged,
    /// "&#9650; 3" when the baseline was zero (no percent to show), "&#9650; 2.1 pp" for a percent-unit metric
    /// (the absolute part is in percentage points). Null is an en dash: no baseline to compare against.
    /// Rendered in neutral text; the glyph carries the direction, not colour.
    /// </summary>
    public static string Delta(Model.Delta? delta, MetricUnit unit)
    {
        if (delta is null)
        {
            return NoDelta;
        }

        var d = delta.Value;
        if (d.Absolute == 0)
        {
            return "= 0";
        }

        var glyph = d.Absolute > 0 ? "▲" : "▼"; // ▲ ▼
        var magnitude = unit == MetricUnit.Percent
            ? Round1(Math.Abs(d.Absolute)) + " pp"
            : Round(Math.Abs(d.Absolute)).ToString("N0", CultureInfo.InvariantCulture);

        var text = $"{glyph} {magnitude}";

        if (d.Percent is not null)
        {
            var sign = d.Percent.Value < 0 ? "−" : "+"; // − (minus sign) or ASCII plus
            text += $" ({sign}{Round1(Math.Abs(d.Percent.Value))}%)";
        }

        return text;
    }

    /// <summary>"3/7 days", shown next to a seven-day-mean comparison built from fewer than seven days. Null when the baseline is complete.</summary>
    public static string? BaselineNote(int baselineDaysPresent) =>
        baselineDaysPresent < 7 ? $"{baselineDaysPresent}/7 days" : null;

    /// <summary>A full day name and date, invariant and English regardless of the host culture: "Thursday 10 September 2026".</summary>
    public static string DayName(DateOnly date) => date.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The report date as "yyyy-MM-dd", for the subject line and the "sent"/"Generated" footer lines.</summary>
    public static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static double Round(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    private static string Round1(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
