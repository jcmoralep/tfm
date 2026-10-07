using BmadPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BmadPlatform.Infrastructure.Tests.Persistence;

/// <summary>
/// Model-level checks. No database is needed: the MySQL server version is explicit,
/// so building the model and comparing it with the migration snapshot never opens a connection.
/// </summary>
public sealed class ApplicationDbContextModelTests : IDisposable
{
    // Never used to connect; it only satisfies the provider configuration.
    private const string UnusedConnectionString = "Server=localhost;Database=bmadplatform_tests;User=tests";

    private readonly ServiceProvider provider;

    public ApplicationDbContextModelTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = UnusedConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        provider = services.BuildServiceProvider();
    }

    [Fact]
    public void Runtime_model_has_no_changes_missing_from_the_migrations()
    {
        using var context = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Design_time_model_has_no_changes_missing_from_the_migrations()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Model_contains_identity_user_tables_and_no_role_tables()
    {
        using var context = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();

        var tables = context.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal<string>(["AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetUsers"], tables);
    }

    [Fact]
    public void Initial_migration_is_present()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_InitialIdentity", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_connection_string_fails_fast_with_the_variable_name()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(new ConfigurationBuilder().Build()));

        Assert.Contains("ConnectionStrings__DefaultConnection", exception.Message);
    }

    public void Dispose() => provider.Dispose();
}
