using DailyReport.Core.Model;

namespace DailyReport.Core.Render;

/// <summary>Small pieces shared by <see cref="HtmlRenderer"/> and <see cref="TextRenderer"/> so the two stay in sync.</summary>
internal static class RenderSupport
{
    /// <summary>Status shown with both a glyph and a word, never colour alone.</summary>
    public static string StatusPill(CheckStatus status) => status switch
    {
        CheckStatus.Fail => "🔴 FAIL",
        CheckStatus.Error => "❗ ERROR",
        CheckStatus.Warn => "🟠 WARN",
        CheckStatus.Pass => "✅ PASS",
        CheckStatus.Skipped => "⏭ SKIPPED",
        _ => status.ToString(),
    };

    /// <summary>Game key -> display name, so every part of the report can say "Competitive Crosswords" instead of "crosswords".</summary>
    public static IReadOnlyDictionary<string, string> GameNames(Report report) =>
        report.Games.ToDictionary(g => g.GameKey, g => g.Name, StringComparer.Ordinal);

    /// <summary>The game's <see cref="Model.Report.Games"/> name for a key, or the key itself when no section exists for it.</summary>
    public static string GameName(IReadOnlyDictionary<string, string> names, string key) =>
        names.TryGetValue(key, out var name) ? name : key;

    /// <summary>Elapsed time as whole milliseconds, e.g. "42 ms".</summary>
    public static string ElapsedMs(TimeSpan elapsed) =>
        Math.Round(elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " ms";

    /// <summary>"1 warning" / "2 warnings".</summary>
    public static string Pluralise(int count, string singular, string plural) => count == 1 ? singular : plural;

    /// <summary>Every source failure logged against a given game key, in list order.</summary>
    public static IReadOnlyList<SourceFailure> FailuresFor(Report report, string gameKey) =>
        report.Failures.Where(f => f.GameKey == gameKey).ToList();
}
