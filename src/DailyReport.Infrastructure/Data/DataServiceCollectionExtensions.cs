using DailyReport.Infrastructure.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyReport.Infrastructure.Data;

/// <summary>
/// Hands out read-only contexts on demand. Providers depend on this rather than on EF's factory so that a
/// missing REPORT_DB_CONNECTION is discovered when a section collects, not when the host starts: the email
/// still goes out, with that section red.
/// </summary>
public interface IGamesDatabase
{
    GamesDbContext CreateContext();
}

public static class DataServiceCollectionExtensions
{
    public const string ConnectionStringName = "Games";

    public static IServiceCollection AddGamesDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextFactory<GamesDbContext>((_, options) =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new MissingSecretException("REPORT_DB_CONNECTION");
            }

            options.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(60));
        });

        services.AddSingleton<IGamesDatabase, LazyGamesDatabase>();
        return services;
    }

    private sealed class LazyGamesDatabase(IServiceProvider services) : IGamesDatabase
    {
        public GamesDbContext CreateContext() =>
            services.GetRequiredService<IDbContextFactory<GamesDbContext>>().CreateDbContext();
    }
}
