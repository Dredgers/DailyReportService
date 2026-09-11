namespace DailyReport.Core.Model;

public enum CheckStatus
{
    Pass,

    /// <summary>Worth a look, not an outage (a low puzzle queue). Renders amber.</summary>
    Warn,

    /// <summary>The check ran and the site is wrong. Renders red, at the top.</summary>
    Fail,

    /// <summary>Not applicable today (a weekly probe on a weekday, a game with no WebSocket).</summary>
    Skipped,

    /// <summary>The probe itself could not complete (timeout, exception). Renders red, at the top: we do not know.</summary>
    Error,
}

public sealed record CheckResult(string GameKey, string Name, CheckStatus Status, string Detail, TimeSpan Elapsed)
{
    public bool IsRed => Status is CheckStatus.Fail or CheckStatus.Error;

    public static CheckResult Pass(string gameKey, string name, string detail, TimeSpan elapsed) => new(gameKey, name, CheckStatus.Pass, detail, elapsed);

    public static CheckResult Warn(string gameKey, string name, string detail, TimeSpan elapsed) => new(gameKey, name, CheckStatus.Warn, detail, elapsed);

    public static CheckResult Fail(string gameKey, string name, string detail, TimeSpan elapsed) => new(gameKey, name, CheckStatus.Fail, detail, elapsed);

    public static CheckResult Skipped(string gameKey, string name, string detail) => new(gameKey, name, CheckStatus.Skipped, detail, TimeSpan.Zero);

    public static CheckResult Error(string gameKey, string name, string detail, TimeSpan elapsed) => new(gameKey, name, CheckStatus.Error, detail, elapsed);
}

/// <summary>A metrics section that could not be produced. The email still goes out; this renders red instead of the numbers.</summary>
public sealed record SourceFailure(string GameKey, string Section, string Summary);
