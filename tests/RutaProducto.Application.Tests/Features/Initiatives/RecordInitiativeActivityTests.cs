using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Domain.Initiatives;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Tests.Features.Initiatives;

public sealed class RecordInitiativeActivityTests
{
    private readonly InitiativeTestContext context = new();

    private Task Record(Guid id) =>
        new RecordInitiativeActivityCommandHandler(context.Repository, context.User, context.Clock)
            .Handle(new RecordInitiativeActivityCommand(id), default);

    private Initiative Stored(Guid id) => context.Repository.Stored.Single(i => i.Id == id);

    [Theory]
    [InlineData(0, false)]
    [InlineData(59, false)]
    [InlineData(60, true)] // exactly one minute is no longer "recent"
    [InlineData(3600, true)]
    public async Task Activity_is_written_only_when_the_last_one_is_a_minute_old_or_more(int secondsSince, bool written)
    {
        var id = await context.SaveDetails(null, "App");
        var created = context.Clock.GetUtcNow();
        context.Clock.Advance(TimeSpan.FromSeconds(secondsSince));

        await Record(id);

        var stored = Stored(id);
        Assert.Equal(written ? created.AddSeconds(secondsSince) : created, stored.LastActivityAt);
        Assert.Equal(created, stored.UpdatedAt); // the initiative itself did not change
        Assert.Equal(written ? 1 : 0, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Another_owners_or_unknown_initiative_is_ignored_without_error_or_write()
    {
        var id = await context.SaveDetails(null, "App");
        var before = Stored(id).LastActivityAt;
        context.Clock.Advance(TimeSpan.FromHours(1));
        var updates = context.Repository.UpdateCount;

        context.User.UserId = InitiativeTestContext.UserB;
        await Record(id);
        context.User.UserId = InitiativeTestContext.UserA;
        await Record(Guid.NewGuid());

        Assert.Equal(before, Stored(id).LastActivityAt);
        Assert.Equal(updates, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task A_deleted_initiative_is_ignored()
    {
        var id = await context.SaveDetails(null, "App");
        await context.Delete(id);
        var updates = context.Repository.UpdateCount;
        context.Clock.Advance(TimeSpan.FromHours(1));

        await Record(id);

        Assert.Equal(updates, context.Repository.UpdateCount);
    }

    [Fact]
    public void The_command_prints_only_its_name()
    {
        Assert.Equal(nameof(RecordInitiativeActivityCommand), new RecordInitiativeActivityCommand(Guid.NewGuid()).ToString());
    }

    // ---- The helper other modules use ----

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(NotFoundException))]
    public async Task A_failure_is_swallowed_and_logged_with_only_the_id_and_the_exception_type(Type exceptionType)
    {
        const string Secret = "contenido-privado-del-usuario";
        var logs = new CapturingLoggerProvider();
        var logger = LoggerFactory.Create(builder => builder.AddProvider(logs)).CreateLogger("test");
        var sender = new InitiativesSender(context.Repository, context.User, context.Clock)
        {
            BeforeHandle = _ => throw (Exception)Activator.CreateInstance(exceptionType, Secret)!,
        };
        var id = Guid.NewGuid();

        await sender.RecordActivityAsync(logger, id, default);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Contains(id.ToString(), entry.AllText, StringComparison.Ordinal);
        Assert.Contains(exceptionType.Name, entry.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, entry.AllText, StringComparison.Ordinal);
        Assert.Equal(
            ["ExceptionType", "InitiativeId"],
            entry.Properties.Select(p => p.Key).Where(k => !k.StartsWith('{')).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task Cancellation_is_not_swallowed(Type exceptionType)
    {
        var logs = new CapturingLoggerProvider();
        var logger = LoggerFactory.Create(builder => builder.AddProvider(logs)).CreateLogger("test");
        var sender = new InitiativesSender(context.Repository, context.User, context.Clock)
        {
            BeforeHandle = _ => throw (Exception)Activator.CreateInstance(exceptionType)!,
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.RecordActivityAsync(logger, Guid.NewGuid(), default));

        Assert.Empty(logs.Entries);
    }
}
