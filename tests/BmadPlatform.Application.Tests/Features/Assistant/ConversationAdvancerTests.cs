using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class ConversationAdvancerTests
{
    private readonly AssistantTestContext context = new();

    [Fact]
    public async Task A_stored_confirmation_that_was_not_applied_completes_on_start_without_a_second_confirmation()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        context.Sender.BeforeHandle = request =>
        {
            if (request is StartPlanningCommand)
            {
                throw new InvalidOperationException("boom");
            }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes));
        Assert.Equal(InitiativeStatus.Clarifying, (await context.Details(id)).Status);
        Assert.True((await context.Current(id)).NeedsResume);
        context.Sender.BeforeHandle = null;

        var resumed = await context.Start(id);

        Assert.Equal(InitiativeStatus.Planning, (await context.Details(id)).Status);
        Assert.Equal(1, context.StoredMessages(id).Count(m => m.QuickReplyKey == AssistantScript.ReplyKeys.ConfirmYes));
        Assert.Equal(AssistantScript.Keys.Capabilities, context.StoredMessages(id)[^1].TopicKey);
        Assert.False(resumed.NeedsResume);

        // Resuming again changes nothing.
        var saves = context.Conversations.SaveCount;
        await context.Start(id);
        Assert.Equal(saves, context.Conversations.SaveCount);
    }

    [Fact]
    public async Task A_confirmation_whose_reply_failed_is_resumed_with_one_reply_and_planning()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        context.Assistant.FailuresRemaining = 1;

        await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes));

        // The transition already happened before the reply was requested.
        Assert.Equal(InitiativeStatus.Planning, (await context.Details(id)).Status);
        Assert.Equal(MessageRole.User, context.StoredMessages(id)[^1].Role);

        await context.Start(id);

        Assert.Equal(AssistantScript.Keys.Capabilities, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(1, context.StoredMessages(id).Count(m => m.QuickReplyKey == AssistantScript.ReplyKeys.ConfirmYes));
    }

    [Fact]
    public async Task A_pending_reply_is_produced_once_when_two_tabs_resume_together()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);
        context.Assistant.FailuresRemaining = 1;
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Send(id, opened.Version, "Idea"));

        // The other tab resumes while this one is waiting for the service.
        context.Assistant.OnNextCall = () => context.Start(id);

        var view = await context.Start(id);

        var replies = context.StoredMessages(id).Where(m => m.Role == MessageRole.Assistant && m.TopicKey == AssistantScript.Keys.Users);
        Assert.Single(replies);
        Assert.Equal(AssistantScript.Keys.Users, view.Journey.NextTopic!.Key);
        Assert.Equal([1, 2, 3], context.StoredMessages(id).Select(m => m.Sequence));
    }

    [Fact]
    public async Task The_closing_message_is_added_once()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Small);
        var view = await context.AnswerUntilDecision(id);
        await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);
        var messages = context.StoredMessages(id).Count;

        await context.Start(id);
        await context.Start(id);

        Assert.Equal(messages, context.StoredMessages(id).Count);
        Assert.Equal(1, context.StoredMessages(id).Count(m => m.TopicKey == AssistantScript.Keys.Closing));
    }

    [Fact]
    public async Task Planning_complete_ends_with_the_closing_message_and_the_status_stays_planning()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        view = await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);

        for (var turn = 0; turn < 30 && view.Journey.NextTopic is { Kind: TopicKind.Question } topic; turn++)
        {
            view = await context.Send(id, view.Version, $"respuesta {topic.Key}");
        }

        Assert.Equal(AssistantScript.Keys.Closing, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(InitiativeStatus.Planning, view.Status);
        Assert.False(view.CanSend);
        Assert.Equal("8 de 8 temas cubiertos", view.Journey.ProgressText);
    }

    [Fact]
    public async Task A_level_edit_in_clarifying_is_picked_up_on_the_next_start_without_losing_history()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        await context.Start(id);
        await context.Answer(id, "Idea");
        await context.Answer(id, "Usuarios");
        await context.Answer(id, "Problema");
        var history = context.StoredMessages(id).Count;
        Assert.Equal(AssistantScript.Keys.Success, context.StoredMessages(id)[^1].TopicKey);

        await context.Initiatives.Update(id, "App", null, DepthMode.Manual, InitiativeDepth.Small);
        var stale = await context.Current(id);
        Assert.True(stale.NeedsResume);
        Assert.False(stale.CanSend);

        var view = await context.Start(id);

        Assert.Equal(history + 1, context.StoredMessages(id).Count);
        Assert.Equal(AssistantScript.Keys.Capabilities, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(AssistantScript.Keys.Capabilities, view.Journey.NextTopic!.Key);
        Assert.True(view.CanSend);
    }

    [Fact]
    public async Task A_reply_that_loses_the_race_is_dropped_and_the_stored_one_is_returned()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);

        // While this tab waits for the service, another tab resumes and stores the reply first.
        context.Assistant.OnNextCall = () => context.Start(id);

        var view = await context.Send(id, opened.Version, "Idea");

        var replies = context.StoredMessages(id).Where(m => m.Role == MessageRole.Assistant && m.TopicKey == AssistantScript.Keys.Users);
        Assert.Single(replies);
        Assert.Equal(3, view.Messages.Count);
        Assert.Equal(AssistantScript.Keys.Users, view.Journey.NextTopic!.Key);
        Assert.True(view.CanSend);
    }
}
