using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Infrastructure.Delivery.Resend;

public static class ResendServiceCollectionExtensions
{
    /// <summary>Binds <see cref="ResendOptions"/> from the "Resend" section and registers <see cref="IEmailSender"/>
    /// as <see cref="ResendEmailSender"/>. Retries on 5xx/408/429 and transient errors are safe because every
    /// send carries its own idempotency key.</summary>
    public static IServiceCollection AddResend(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.SectionName));

        // No HttpClient.Timeout: the standard resilience handler's total timeout (30 s) is the only clock, so a stall
        // surfaces as Polly's TimeoutRejectedException (an ordinary exception the runner turns into a red line) and
        // never as a stray OperationCanceledException racing our own tokens.
        services.AddHttpClient(ResendEmailSender.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DailyReportService/1.0 (+https://github.com/Dredgers/DailyReportService)");
            })
            .AddStandardResilienceHandler();

        services.AddSingleton<IEmailSender, ResendEmailSender>();

        return services;
    }
}
