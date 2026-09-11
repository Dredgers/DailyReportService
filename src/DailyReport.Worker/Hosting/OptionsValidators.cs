using DailyReport.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DailyReport.Worker.Hosting;

public sealed class ReportOptionsValidator : IValidateOptions<ReportOptions>
{
    public ValidateOptionsResult Validate(string? name, ReportOptions options)
    {
        var errors = OptionsValidation.Validate(options);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class GamesOptionsValidator : IValidateOptions<GamesOptions>
{
    public ValidateOptionsResult Validate(string? name, GamesOptions options)
    {
        var errors = OptionsValidation.Validate(options);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
