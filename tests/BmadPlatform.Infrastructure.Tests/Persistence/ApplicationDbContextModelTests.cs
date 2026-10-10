using System.Linq.Expressions;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Infrastructure.Assistant;
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
    public void Model_contains_identity_user_tables_the_module_tables_and_no_role_tables()
    {
        using var context = CreateContext();

        var tables = context.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal<string>(
            ["AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetUsers", "asi_conversations", "asi_messages", "ini_initiatives"],
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
    public void Conversation_is_unique_per_initiative_and_has_a_version_concurrency_token_and_no_foreign_key_outside_the_module()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Conversation))!;

        var index = Assert.Single(entityType.GetIndexes());
        Assert.True(index.IsUnique);
        Assert.Equal([nameof(Conversation.InitiativeId)], index.Properties.Select(property => property.Name));

        Assert.True(entityType.FindProperty(nameof(Conversation.Version))!.IsConcurrencyToken);
        Assert.Equal(255, entityType.FindProperty(nameof(Conversation.OwnerId))!.GetMaxLength());

        // The only relationship is the one owned by the module: from messages to this conversation.
        Assert.Empty(entityType.GetForeignKeys());
        Assert.Equal(typeof(Message), Assert.Single(entityType.GetNavigations()).TargetEntityType.ClrType);
    }

    [Fact]
    public void Message_sequence_is_unique_per_conversation_and_its_only_foreign_key_targets_the_conversation()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Message))!;

        var index = Assert.Single(entityType.GetIndexes());
        Assert.True(index.IsUnique);
        Assert.Equal(
            [nameof(Message.ConversationId), nameof(Message.Sequence)],
            index.Properties.Select(property => property.Name));

        var foreignKey = Assert.Single(entityType.GetForeignKeys());
        Assert.Equal(typeof(Conversation), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Message_enums_are_strings_and_keys_and_content_have_the_domain_lengths()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Message))!;

        foreach (var name in new[] { nameof(Message.Role), nameof(Message.AnswerKind) })
        {
            var property = entityType.FindProperty(name)!;

            Assert.Equal(typeof(string), property.GetProviderClrType());
            Assert.Equal(20, property.GetMaxLength());
        }

        Assert.True(entityType.FindProperty(nameof(Message.AnswerKind))!.IsNullable);
        Assert.Equal(Message.ContentMaxLength, entityType.FindProperty(nameof(Message.Content))!.GetMaxLength());
        Assert.Equal(Message.KeyMaxLength, entityType.FindProperty(nameof(Message.TopicKey))!.GetMaxLength());
        Assert.Equal(Message.KeyMaxLength, entityType.FindProperty(nameof(Message.QuickReplyKey))!.GetMaxLength());
    }

    [Fact]
    public void Message_quick_replies_are_a_json_column_that_round_trips_through_the_converter()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(Message))!.FindProperty(nameof(Message.QuickReplies))!;

        Assert.Equal("json", property.GetColumnType());

        var converter = property.GetValueConverter()!;
        IReadOnlyList<QuickReply> replies = [new QuickReply("a", "Sí \"quoted\" ñ"), new QuickReply("b", "No sé")];

        var json = (string)converter.ConvertToProvider(replies)!;
        var restored = (IReadOnlyList<QuickReply>)converter.ConvertFromProvider(json)!;

        Assert.Equal(replies, restored);
        Assert.Empty((IReadOnlyList<QuickReply>)converter.ConvertFromProvider("[]")!);
    }

    [Fact]
    public void Message_quick_replies_comparer_compares_by_content_and_snapshots_a_copy()
    {
        using var context = CreateContext();
        var comparer = context.Model.FindEntityType(typeof(Message))!
            .FindProperty(nameof(Message.QuickReplies))!.GetValueComparer()!;

        IReadOnlyList<QuickReply> one = [new QuickReply("a", "A")];
        IReadOnlyList<QuickReply> same = [new QuickReply("a", "A")];
        IReadOnlyList<QuickReply> other = [new QuickReply("a", "B")];

        Assert.True(comparer.Equals(one, same));
        Assert.False(comparer.Equals(one, other));
        Assert.Equal(comparer.GetHashCode(one), comparer.GetHashCode(same));

        var snapshot = (IReadOnlyList<QuickReply>)comparer.Snapshot(one)!;
        Assert.NotSame(one, snapshot);
        Assert.Equal(one, snapshot);
    }

    [Fact]
    public void Conversation_derived_views_are_not_mapped_as_relationships()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Conversation))!;

        Assert.Null(entityType.FindNavigation(nameof(Conversation.VisibleMessages)));
        Assert.Null(entityType.FindProperty(nameof(Conversation.CanUndo)));
    }

    [Fact]
    public void Assistant_migration_is_present()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_AddAssistantConversations", StringComparison.Ordinal));
    }

    [Fact]
    public void Repository_is_registered_per_scope_for_the_application_port()
    {
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<BmadPlatform.Application.Features.Assistant.IConversationRepository>();

        Assert.IsType<ConversationRepository>(repository);
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
