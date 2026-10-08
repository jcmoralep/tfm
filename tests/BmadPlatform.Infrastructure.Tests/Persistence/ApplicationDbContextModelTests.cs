using System.Linq.Expressions;
using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Infrastructure.Initiatives;
using BmadPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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
    public void Model_contains_identity_user_tables_the_initiatives_table_and_no_role_tables()
    {
        using var context = CreateContext();

        var tables = context.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal<string>(
            ["AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetUsers", "ini_initiatives"],
            tables);
    }

    [Fact]
    public void Every_table_outside_identity_carries_a_module_prefix()
    {
        using var context = CreateContext();

        var tables = context.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName() ?? string.Empty)
            .Where(table => !table.StartsWith("AspNet", StringComparison.Ordinal));

        Assert.All(tables, table => Assert.Matches("^[a-z]{2,}_[a-z_]+$", table));
    }

    [Fact]
    public void Initiative_has_a_single_soft_delete_query_filter()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Initiative))!;

        var filter = entityType.GetQueryFilter();

        Assert.NotNull(filter);

        var matches = ((Expression<Func<Initiative, bool>>)filter).Compile();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var active = Initiative.CreateDraft("owner", "Active", null, now);
        var deleted = Initiative.CreateDraft("owner", "Deleted", null, now);
        deleted.Delete(now);

        Assert.True(matches(active));
        Assert.False(matches(deleted));
    }

    [Fact]
    public void Initiative_enums_are_stored_as_strings()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Initiative))!;

        foreach (var name in new[] { nameof(Initiative.Status), nameof(Initiative.DepthMode), nameof(Initiative.Depth), nameof(Initiative.CreationStep) })
        {
            var property = entityType.FindProperty(name)!;

            Assert.Equal(typeof(string), property.GetProviderClrType());
            Assert.Equal(20, property.GetMaxLength());
        }

        Assert.False(entityType.FindProperty(nameof(Initiative.Status))!.IsNullable);
        Assert.False(entityType.FindProperty(nameof(Initiative.CreationStep))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(Initiative.DepthMode))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(Initiative.Depth))!.IsNullable);
    }

    [Fact]
    public void Initiative_name_uses_the_accent_insensitive_collation_and_domain_lengths()
    {
        using var context = CreateContext();
        // Collation is only kept in the design-time model.
        var entityType = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Initiative))!;

        var name = entityType.FindProperty(nameof(Initiative.Name))!;

        Assert.Equal("utf8mb4_0900_ai_ci", name.GetCollation());
        Assert.Equal(Initiative.NameMaxLength, name.GetMaxLength());
        Assert.Equal(Initiative.DescriptionMaxLength, entityType.FindProperty(nameof(Initiative.Description))!.GetMaxLength());
    }

    [Fact]
    public void Initiative_owner_is_a_plain_string_without_foreign_key_and_the_list_index_exists()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Initiative))!;

        Assert.Empty(entityType.GetForeignKeys());
        Assert.Equal(255, entityType.FindProperty(nameof(Initiative.CreatedByUserId))!.GetMaxLength());

        var index = Assert.Single(entityType.GetIndexes());
        Assert.Equal(
            [nameof(Initiative.CreatedByUserId), nameof(Initiative.UpdatedAt)],
            index.Properties.Select(property => property.Name));
    }

    [Fact]
    public void Initiative_search_translates_to_an_escaped_like_with_the_soft_delete_predicate()
    {
        using var context = CreateContext();
        var pattern = LikePattern.Contains("100%");

        var sql = context.Set<Initiative>()
            .Where(initiative => EF.Functions.Like(initiative.Name, pattern, LikePattern.EscapeCharacter))
            .ToQueryString();

        Assert.Contains("LIKE", sql);
        Assert.Contains("ESCAPE", sql);
        Assert.Contains("`DeletedAt` IS NULL", sql);
    }

    [Fact]
    public void Initiative_dates_are_stored_as_datetime_with_microseconds()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Initiative))!;

        foreach (var name in new[] { nameof(Initiative.CreatedAt), nameof(Initiative.UpdatedAt), nameof(Initiative.DeletedAt) })
        {
            Assert.Equal("datetime(6)", entityType.FindProperty(name)!.GetRelationalTypeMapping().StoreType);
        }
    }

    [Fact]
    public void Initial_migration_is_present()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_InitialIdentity", StringComparison.Ordinal));
    }

    [Fact]
    public void Initiatives_migration_is_present()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_AddInitiatives", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_connection_string_fails_fast_with_the_variable_name()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(new ConfigurationBuilder().Build()));

        Assert.Contains("ConnectionStrings__DefaultConnection", exception.Message);
    }

    private ApplicationDbContext CreateContext() =>
        provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();

    public void Dispose() => provider.Dispose();
}
