using DailyReport.Infrastructure.Secrets;

namespace DailyReport.Tests.Sources.GoatCounter;

internal sealed class FakeSecretReader(IReadOnlyDictionary<string, string> secrets) : ISecretReader
{
    public string Require(string name) => TryGet(name) ?? throw new MissingSecretException(name);

    public string? TryGet(string name) => secrets.TryGetValue(name, out var value) ? value : null;
}
