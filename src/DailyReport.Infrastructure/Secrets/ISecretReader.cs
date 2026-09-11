namespace DailyReport.Infrastructure.Secrets;

/// <summary>Reads a named secret (an environment variable in production). Faked in tests so nothing reads the real environment.</summary>
public interface ISecretReader
{
    /// <summary>Returns the value, or throws <see cref="MissingSecretException"/>. Never returns empty.</summary>
    string Require(string name);

    string? TryGet(string name);
}

public sealed class EnvironmentSecretReader : ISecretReader
{
    public string Require(string name) => TryGet(name) ?? throw new MissingSecretException(name);

    public string? TryGet(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>A missing secret is a source failure for the section that needed it, never a crash of the whole run.</summary>
public sealed class MissingSecretException(string name) : Exception($"Secret '{name}' is not set.")
{
    public string Name { get; } = name;
}
