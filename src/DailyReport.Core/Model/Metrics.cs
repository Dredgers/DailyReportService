namespace DailyReport.Core.Model;

/// <summary>Who a number counts. Never mixed: a headline includes guests; the signed-in line is its own metric.</summary>
public enum Lane
{
    All,
    SignedIn,
}

public enum MetricUnit
{
    Count,
    Percent,
}

/// <summary>
/// Identity and presentation of one metric. <see cref="Group"/> is the heading it renders under (Traffic, Completions, Return, ...).
/// <see cref="Note"/> is the caveat the reader needs ("signed-in players only", "page loads, not visitors").
/// </summary>
public sealed record MetricDefinition(string Key, string Group, string Label, Lane Lane, MetricUnit Unit, string? Note = null);

/// <summary>Raw provider output: one value per day. A null value means "no data for that day", never zero.</summary>
public sealed record DailySeries(MetricDefinition Definition, IReadOnlyDictionary<DateOnly, double?> ByDay)
{
    public double? On(DateOnly day) => ByDay.TryGetValue(day, out var v) ? v : null;
}

/// <summary>A signed change against a baseline. Percent is null when the baseline is zero.</summary>
public readonly record struct Delta(double Absolute, double? Percent)
{
    public static Delta? Between(double? value, double? baseline)
    {
        if (value is null || baseline is null)
        {
            return null;
        }

        var abs = value.Value - baseline.Value;
        return new Delta(abs, baseline.Value == 0 ? null : abs / baseline.Value * 100);
    }
}

/// <summary>One composed line of the report: the value for the game day and its three comparisons.</summary>
public sealed record Metric(
    MetricDefinition Definition,
    double? Value,
    double? DayBefore,
    double? SevenDayMean,
    int BaselineDaysPresent,
    double? SameWeekdayLastWeek)
{
    public Delta? VsDayBefore => Delta.Between(Value, DayBefore);

    public Delta? VsSevenDayMean => Delta.Between(Value, SevenDayMean);

    public Delta? VsSameWeekdayLastWeek => Delta.Between(Value, SameWeekdayLastWeek);
}
