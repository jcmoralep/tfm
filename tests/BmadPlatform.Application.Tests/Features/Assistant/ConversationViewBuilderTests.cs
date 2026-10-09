using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class ConversationViewBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static InitiativeDetails Details(
        InitiativeStatus status = InitiativeStatus.Clarifying,
        DepthMode? mode = DepthMode.Manual,
        InitiativeDepth? depth = InitiativeDepth.Standard) =>
        new(Guid.NewGuid(), "App", null, status, mode, depth, CreationStep.Review, Now, Now);

    private static Conversation Started(params (string Key, string Text)[] exchanges)
    {
        var conversation = Conversation.Start(Guid.NewGuid(), "user-a", Now);
        var topic = AssistantScript.Get(AssistantScript.Keys.Idea);
        conversation.AddAssistantMessage(topic.Key, "pregunta", topic.QuickReplies, Now);

        foreach (var (key, text) in exchanges)
        {
            conversation.AddUserAnswer(key, text, AnswerKind.FreeText, null, false, Now);
        }

        return conversation;
    }

    [Fact]
    public void Without_a_conversation_nothing_can_be_done()
    {
        var view = ConversationViewBuilder.Build(Details(), null);

        Assert.False(view.Started);
        Assert.Equal(0, view.Version);
        Assert.Empty(view.Messages);
        Assert.False(view.CanSend);
        Assert.False(view.CanUndo);
        Assert.False(view.NeedsResume);
    }

    [Fact]
    public void An_open_question_offers_its_quick_replies_with_label_and_key_and_accepts_an_answer()
    {
        var conversation = Started();

        var view = ConversationViewBuilder.Build(Details(), conversation);

        Assert.True(view.Started);
        Assert.Equal(conversation.Version, view.Version);
        Assert.Equal(new QuickReply("unknown", "No sé"), Assert.Single(view.QuickReplies));
        Assert.True(view.CanSend);
        Assert.False(view.CanUndo);
        Assert.False(view.NeedsResume);
        Assert.Equal(AssistantScript.Keys.Idea, view.Journey.NextTopic!.Key);
    }

    [Fact]
    public void Undone_messages_are_not_shown_and_do_not_count()
    {
        var conversation = Started((AssistantScript.Keys.Idea, "Idea"));
        var users = AssistantScript.Get(AssistantScript.Keys.Users);
        conversation.AddAssistantMessage(users.Key, "pregunta", users.QuickReplies, Now);
        conversation.UndoLastAnswer(Now);

        var view = ConversationViewBuilder.Build(Details(), conversation);

        Assert.Single(view.Messages);
        Assert.Equal(AssistantScript.Keys.Idea, view.Journey.NextTopic!.Key);
        Assert.Equal(0, view.Journey.Clarify.Covered);
        Assert.True(view.CanSend);
    }

    [Fact]
    public void A_pending_reply_cannot_send_needs_resume_and_shows_no_buttons()
    {
        var conversation = Started((AssistantScript.Keys.Idea, "Idea"));

        var view = ConversationViewBuilder.Build(Details(), conversation);

        Assert.False(view.CanSend);
        Assert.True(view.NeedsResume);
        Assert.True(view.CanUndo);
        Assert.Empty(view.QuickReplies);
    }

    [Fact]
    public void An_answer_that_changed_the_initiative_cannot_be_undone()
    {
        var conversation = Conversation.Start(Guid.NewGuid(), "user-a", Now);
        var confirmation = AssistantScript.Get(AssistantScript.Keys.ConfirmPlanning);
        conversation.AddAssistantMessage(confirmation.Key, "pregunta", confirmation.QuickReplies, Now);
        conversation.AddUserAnswer(confirmation.Key, "Sí", AnswerKind.QuickReply, AssistantScript.ReplyKeys.ConfirmYes, true, Now);

        var view = ConversationViewBuilder.Build(Details(InitiativeStatus.Planning), conversation);

        Assert.False(view.CanUndo);
        Assert.True(view.NeedsResume);
    }

    [Fact]
    public void A_question_the_level_no_longer_expects_needs_a_resume()
    {
        var conversation = Started();
        var small = Details(depth: InitiativeDepth.Small);
        var planningStandard = Details(InitiativeStatus.Planning);

        // Standard planning asks capabilities first, so the idea question on screen is out of date.
        var view = ConversationViewBuilder.Build(planningStandard, conversation);

        Assert.True(view.NeedsResume);
        Assert.False(view.CanSend);
        Assert.False(ConversationViewBuilder.Build(small, conversation).NeedsResume);
    }

    [Fact]
    public void The_closing_message_accepts_no_answer_and_needs_no_resume()
    {
        var conversation = Conversation.Start(Guid.NewGuid(), "user-a", Now);
        var closing = AssistantScript.Get(AssistantScript.Keys.Closing);
        conversation.AddAssistantMessage(closing.Key, "listo", closing.QuickReplies, Now);

        var view = ConversationViewBuilder.Build(Details(InitiativeStatus.Planning, depth: InitiativeDepth.Small), conversation);

        Assert.False(view.CanSend);
        Assert.False(view.NeedsResume);
        Assert.Empty(view.QuickReplies);
    }

    [Fact]
    public void Ready_to_build_is_read_only_with_the_history_but_no_actions()
    {
        var conversation = Started();

        var view = ConversationViewBuilder.Build(Details(InitiativeStatus.ReadyToBuild), conversation);

        Assert.True(view.ReadOnly);
        Assert.Single(view.Messages);
        Assert.Empty(view.QuickReplies);
        Assert.False(view.CanSend);
        Assert.False(view.CanUndo);
        Assert.False(view.NeedsResume);
    }

    [Fact]
    public void Draft_offers_no_actions()
    {
        var view = ConversationViewBuilder.Build(Details(InitiativeStatus.Draft, mode: null, depth: null), null);

        Assert.False(view.CanSend);
        Assert.False(view.ReadOnly);
        Assert.False(view.NeedsResume);
    }
}
