using BmadPlatform.Infrastructure.Identity.Seeding;

namespace BmadPlatform.Infrastructure.Tests.Persistence;

public sealed class PendingMigrationsGuardTests
{
    [Fact]
    public void No_pending_migrations_does_not_throw()
    {
        PendingMigrationsGuard.EnsureNonePending([]);
    }

    [Fact]
    public void Pending_migrations_throw_with_the_remedy_and_the_migration_names()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => PendingMigrationsGuard.EnsureNonePending(["20260101000000_Initial"]));

        Assert.Contains("dotnet ef database update", exception.Message);
        Assert.Contains("Base de datos", exception.Message);
        Assert.Contains("20260101000000_Initial", exception.Message);
    }
}
