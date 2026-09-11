using DailyReport.Infrastructure.Data;
using DailyReport.Infrastructure.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Tests.Data;

public sealed class DataRegistrationTests
{
    [Test]
    public void Missing_connection_string_surfaces_as_a_missing_secret_only_when_a_context_is_created()
    {
        var services = new ServiceCollection().AddGamesDatabase(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        // Resolving the database is safe at startup...
        var database = provider.GetRequiredService<IGamesDatabase>();

        // ...the secret is demanded when a section actually collects.
        var ex = Assert.Throws<MissingSecretException>(() => database.CreateContext());
        Assert.That(ex!.Name, Is.EqualTo("REPORT_DB_CONNECTION"));
    }

    [Test]
    public void A_connection_string_yields_a_no_tracking_context_without_connecting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Games"] = "Host=localhost;Username=report_ro;Password=x;Database=postgres" })
            .Build();
        var services = new ServiceCollection().AddGamesDatabase(configuration);
        using var provider = services.BuildServiceProvider();

        using var ctx = provider.GetRequiredService<IGamesDatabase>().CreateContext();

        Assert.That(ctx.ChangeTracker.QueryTrackingBehavior, Is.EqualTo(QueryTrackingBehavior.NoTracking));
    }
}
