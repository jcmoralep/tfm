using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class GetConversationTests
{
    private readonly AssistantTestContext context = new();

    [Fact]
    public async Task Before_the_first_visit_the_view_is_not_started_and_nothing_can_be_done()
    {
        var id = await context.Initiatives.CreateClarifying("App");

        var view = await context.Get(id);

        Assert.NotNull(view);
        Assert.False(view.Started);
        Assert.Empty(view.Messages);
        Assert.Empty(view.QuickReplies);
        Assert.False(view.CanSend);
        Assert.False(view.CanUndo);
        Assert.False(view.NeedsResume);
        Assert.Equal(0, view.Version);
        Assert.Equal("App", view.InitiativeName);
        Assert.Empty(context.Conversations.Stored);
    }

    [Fact]
    public async Task Reading_never_writes_or_asks_the_assistant()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        var saves = context.Conversations.SaveCount;
        var requests = context.Assistant.Requests.Count;

        await context.Get(id);
        await context.Get(id);

        Assert.Equal(saves, context.Conversations.SaveCount);
        Assert.Equal(requests, context.Assistant.Requests.Count);
    }

    [Fact]
    public async Task Returns_the_history_in_order_with_the_progress()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        await context.Start(id);
        await context.Answer(id, "Una idea");

        var view = await context.Get(id);

        Assert.NotNull(view);
        Assert.Equal([MessageRole.Assistant, MessageRole.User, MessageRole.Assistant], view.Messages.Select(m => m.Role));
        Assert.Equal([1, 2, 3], view.Messages.Select(m => m.Sequence));
        Assert.Equal("1 de 8 temas cubiertos", view.Journey.ProgressText);
    }

    [Fact]
    public async Task Ready_to_build_is_read_only_but_still_readable()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        context.Initiatives.ForceStatus(id, InitiativeStatus.ReadyToBuild);

        var view = await context.Get(id);

        Assert.NotNull(view);
        Assert.True(view.ReadOnly);
        Assert.Single(view.Messages);
        Assert.Empty(view.QuickReplies);
        Assert.False(view.CanSend);
        Assert.False(view.CanUndo);
        Assert.False(view.NeedsResume);
    }

    [Fact]
    public async Task Draft_reads_as_not_started()
    {
        var id = await context.Initiatives.CreateDraft("Borrador");

        var view = await context.Get(id);

        Assert.NotNull(view);
        Assert.False(view.Started);
        Assert.False(view.CanSend);
        Assert.False(view.NeedsResume);
    }

    [Fact]
    public async Task Foreign_deleted_and_unknown_initiatives_read_as_null()
    {
        var foreign = await context.Initiatives.CreateClarifying("De A");
        await context.Start(foreign);
        var deleted = await context.Initiatives.CreateClarifying("Borrada");
        await context.Initiatives.Delete(deleted);

        context.User.UserId = AssistantTestContext.UserB;
        Assert.Null(await context.Get(foreign));
        context.User.UserId = AssistantTestContext.UserA;
        Assert.Null(await context.Get(deleted));
        Assert.Null(await context.Get(Guid.NewGuid()));
    }

    [Fact]
    public async Task Without_a_current_user_it_fails()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        context.User.UserId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Get(id));
    }
}
