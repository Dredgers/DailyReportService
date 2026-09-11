namespace DailyReport.Infrastructure.Games.Shared;

// Shapes for EF Core's SqlQuery<T>: settable properties named exactly like the quoted column aliases.

public sealed class DayCountRow
{
    public DateOnly Day { get; set; }

    public int N { get; set; }
}

public sealed class DayKeyCountRow
{
    public DateOnly Day { get; set; }

    public string Key { get; set; } = "";

    public int N { get; set; }
}

public sealed class PlayerDayRow
{
    public Guid PlayerId { get; set; }

    public DateOnly Day { get; set; }
}
