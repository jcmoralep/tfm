using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>
    /// Applies the pending EF Core migrations to the configured database and returns their names (empty when it was up to date).
    /// Used by the <c>--migrate</c> command-line mode; normal startup never migrates.
    /// </summary>
    public static async Task<IReadOnlyList<string>> MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);

        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count > 0)
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        return pending;
    }
}
