using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Tests.TestDoubles;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class StartConversationTests
{
    private readonly AssistantTestContext context = new();

    [Fact]
    public async Task First_start_creates_the_conversation_with_the_opening_question()
    {
        var id = await context.Initiatives.CreateClarifying("App");

        var view = await context.Start(id);

        var message = Assert.Single(context.StoredMessages(id));
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal(AssistantScript.Keys.Idea, message.TopicKey);
        Assert.Equal(ScriptedAssistantService.Marker(AssistantScript.Keys.Idea), message.Content);
        Assert.Contains(message.QuickReplies, r => r.Key == AssistantScript.ReplyKeys.Unknown && r.Label == "No sé");
        Assert.Equal(1, context.Conversations.AddCount);
        Assert.True(view.Started);
        Assert.True(view.CanSend);
        Assert.False(view.CanUndo);
        Assert.False(view.NeedsResume);
        Assert.Equal(message.QuickReplies, view.QuickReplies);
        Assert.Equal(context.StoredConversation(id).Version, view.Version);
    }

    [Fact]
    public async Task The_service_receives_only_the_next_topic_and_text_data()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Large);

        await context.Start(id);

        var request = Assert.Single(context.Assistant.Requests);
        Assert.Equal(AssistantScript.Keys.Idea, request.NextTopic.Key);
        Assert.Equal("App", request.Initiative.Name);
        Assert.Equal(InitiativeDepth.Large, request.Initiative.Depth);
        Assert.Empty(request.History);
        Assert.False(request.OffersLevelChoice);
    }

    [Fact]
    public async Task Second_start_is_a_no_op()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var first = await context.Start(id);
        var saves = context.Conversations.SaveCount;

        var second = await context.Start(id);

        Assert.Single(context.StoredMessages(id));
        Assert.Equal(first.Version, second.Version);
        Assert.Equal(1, context.Conversations.AddCount);
        Assert.Equal(saves, context.Conversations.SaveCount);
        Assert.Single(context.Assistant.Requests);
    }

    [Fact]
    public async Task Two_tabs_starting_together_leave_one_conversation_and_one_opening_question()
    {
        var id = await context.Initiatives.CreateClarifying("App");

        // The other tab creates and opens the conversation between our read (nothing) and our add.
        context.Conversations.BeforeNextAdd = () => context.Start(id);

        var view = await context.Start(id);

        Assert.Single(context.Conversations.Stored);
        Assert.Single(context.StoredMessages(id));
        Assert.Single(view.Messages);
    }

    [Fact]
    public async Task Two_tabs_resuming_an_empty_conversation_store_one_opening_question()
    {
        var id = await context.Initiatives.CreateClarifying("App");

        // The first start has created the conversation and is about to phrase the opening when the second one runs.
        context.Assistant.OnNextCall = () => context.Start(id);

        var view = await context.Start(id);

        Assert.Single(context.StoredMessages(id));
        Assert.Single(view.Messages);
        Assert.Equal(2, context.Assistant.Requests.Count);
    }

    [Fact]
    public async Task Draft_cannot_start_and_nothing_is_stored()
    {
        var id = await context.Initiatives.CreateDraft("Borrador");

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Start(id));

        Assert.Equal("Termine de crear la iniciativa para abrir el asistente.", error.Message);
        Assert.Empty(context.Conversations.Stored);
        Assert.Empty(context.Assistant.Requests);
    }

    [Fact]
    public async Task Ready_to_build_cannot_start_with_or_without_a_conversation()
    {
        var withConversation = await context.Initiatives.CreateClarifying("Con conversación");
        await context.Start(withConversation);
        var without = await context.Initiatives.CreateClarifying("Sin conversación");
        context.Initiatives.ForceStatus(withConversation, InitiativeStatus.ReadyToBuild);
        context.Initiatives.ForceStatus(without, InitiativeStatus.ReadyToBuild);
        var requests = context.Assistant.Requests.Count;

        foreach (var id in new[] { withConversation, without })
        {
            var error = await Assert.ThrowsAsync<DomainException>(() => context.Start(id));
            Assert.Equal("La iniciativa ya está lista para construir; la conversación es solo de lectura.", error.Message);
        }

        Assert.Single(context.Conversations.Stored);
        Assert.Equal(requests, context.Assistant.Requests.Count);
    }

    [Fact]
    public async Task Planning_can_start_and_opens_on_the_first_planning_topic()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        await context.Initiatives.StartPlanning(id);

        var view = await context.Start(id);

        Assert.Equal(InitiativeStatus.Planning, view.Status);
        Assert.Equal(AssistantScript.Keys.Capabilities, Assert.Single(context.StoredMessages(id)).TopicKey);
        Assert.True(view.CanSend);
    }

    [Fact]
    public async Task Foreign_deleted_and_unknown_initiatives_are_not_found_and_nothing_is_stored()
    {
        var foreign = await context.Initiatives.CreateClarifying("De A");
        var deleted = await context.Initiatives.CreateClarifying("Borrada");
        await context.Initiatives.Delete(deleted);

        context.User.UserId = AssistantTestContext.UserB;
        var foreignError = await Assert.ThrowsAsync<NotFoundException>(() => context.Start(foreign));
        context.User.UserId = AssistantTestContext.UserA;
        var deletedError = await Assert.ThrowsAsync<NotFoundException>(() => context.Start(deleted));
        var unknownError = await Assert.ThrowsAsync<NotFoundException>(() => context.Start(Guid.NewGuid()));

        Assert.Equal(foreignError.Message, deletedError.Message);
        Assert.Equal(foreignError.Message, unknownError.Message);
        Assert.Empty(context.Conversations.Stored);
        Assert.Equal(0, context.Conversations.AddCount);
    }

    [Fact]
    public async Task Without_a_current_user_it_fails_and_nothing_is_stored()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        context.User.UserId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Start(id));

        Assert.Empty(context.Conversations.Stored);
    }

    [Fact]
    public async Task A_failing_service_on_the_opening_leaves_an_empty_conversation_that_the_next_start_opens()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        context.Assistant.FailuresRemaining = 1;

        await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Start(id));

        Assert.Empty(context.StoredMessages(id));
        var view = await context.Start(id);
        Assert.Single(view.Messages);
        Assert.Single(context.StoredMessages(id));
    }
}
