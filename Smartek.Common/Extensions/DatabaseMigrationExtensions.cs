using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Smartek.Common.Extensions;

/// <summary>
/// Applies EF Core migrations at startup.
/// </summary>
/// <remarks>
/// Needed because nothing else creates the schema: the postgres container is started without
/// POSTGRES_DB, so only the default <c>postgres</c> database exists, while each service points at
/// its own (<c>stagiaire_service_db</c>, …). Without this every request failed with
/// <c>3D000: database "…" does not exist</c>. <see cref="RelationalDatabaseFacadeExtensions.MigrateAsync"/>
/// creates the database when missing and then applies pending migrations.
///
/// Auto-migrating on boot suits this Compose-based setup, where the stack must come up unattended.
/// For a real deployment set <c>Database:AutoMigrate=false</c> and run migrations as a separate
/// step, so two replicas starting at once cannot race on the same schema.
/// </remarks>
public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabaseAsync<TContext>(
        this IHost host,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var configuration = provider.GetRequiredService<IConfiguration>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

        if (!configuration.GetValue("Database:AutoMigrate", true))
        {
            logger.LogInformation("Database:AutoMigrate is false — skipping migrations.");
            return;
        }

        var context = provider.GetRequiredService<TContext>();

        // postgres reports healthy before it finishes accepting connections on a cold volume, so a
        // first attempt can still fail. Retry briefly rather than crash-looping the container.
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

                if (pending.Count == 0)
                {
                    logger.LogInformation("Database schema is up to date.");
                    return;
                }

                logger.LogInformation(
                    "Applying {Count} migration(s): {Migrations}",
                    pending.Count, string.Join(", ", pending));

                await context.Database.MigrateAsync(cancellationToken);

                logger.LogInformation("Migrations applied successfully.");
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(2 * attempt);

                logger.LogWarning(
                    exception,
                    "Migration attempt {Attempt}/{Max} failed; retrying in {Delay}s.",
                    attempt, maxAttempts, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // Out of retries: fail loudly. Serving traffic against a missing schema would turn every
        // request into a 500 that looks like an application bug.
        throw new InvalidOperationException(
            $"Could not apply migrations for {typeof(TContext).Name} after {maxAttempts} attempts.");
    }
}
