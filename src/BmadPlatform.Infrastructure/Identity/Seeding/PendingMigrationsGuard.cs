namespace BmadPlatform.Infrastructure.Identity.Seeding;

/// <summary>
/// Fails startup with an actionable message when the database schema is behind the model,
/// instead of letting seeding crash with a raw "table doesn't exist" error.
/// </summary>
internal static class PendingMigrationsGuard
{
    public static void EnsureNonePending(IReadOnlyCollection<string> pendingMigrations)
    {
        if (pendingMigrations.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The database has {pendingMigrations.Count} pending migration(s): {string.Join(", ", pendingMigrations)}. " +
            "Apply them before starting the application with 'dotnet ef database update' " +
            "(see the \"Base de datos\" section in README.md).");
    }
}
