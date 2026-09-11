using DailyReport.Core.Abstractions;
using DailyReport.Infrastructure.Delivery.Resend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Tests.Delivery;

public sealed class ResendServiceCollectionExtensionsTests
{
    [Test]
    public void AddResend_resolves_an_IEmailSender()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Resend:ApiKey"] = "test" })
            .Build();

        var services = new ServiceCollection().AddResend(configuration).AddLogging();
        using var provider = services.BuildServiceProvider();

        var sender = provider.GetRequiredService<IEmailSender>();

        Assert.That(sender, Is.InstanceOf<ResendEmailSender>());
    }
}
