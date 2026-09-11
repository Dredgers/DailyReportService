using DailyReport.Infrastructure.Sources.GoatCounter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Tests.Sources.GoatCounter;

public sealed class GoatCounterServiceCollectionExtensionsTests
{
    [Test]
    public void AddGoatCounter_resolves_IGoatCounterClient_from_empty_configuration()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection().AddGoatCounter(configuration).AddLogging();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IGoatCounterClient>();

        Assert.That(client, Is.InstanceOf<GoatCounterClient>());
    }
}
