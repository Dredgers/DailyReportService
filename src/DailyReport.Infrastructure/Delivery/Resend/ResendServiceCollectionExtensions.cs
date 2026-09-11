using DailyReport.Core.Abstractions;
using DailyReport.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Infrastructure.Delivery.Resend;

public static class ResendServiceCollectionExtensions
{
    /// <summary>Binds <see cref="ResendOptions"/> from the "Resend" section and registers <see cref="IEmailSender"/>
    /// as <see cref="ResendEmailSender"/>. Retries on 5xx/408/429 and transient errors are safe because every
    /// send carries a per-report-day idempotency key.</summary>
    public static IServiceCollection AddResend(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.SectionName));

        services.AddHttpClient(ResendEmailSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .AddStandardResilienceHandler();

        services.AddSingleton<IEmailSender, ResendEmailSender>();

        return services;
    }
}
