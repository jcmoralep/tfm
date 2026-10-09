using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class ConversationJourneyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly JourneyInput Standard = new(InitiativeStatus.Clarifying, DepthMode.Manual, InitiativeDepth.Standard);
    private static readonly JourneyInput Small = new(InitiativeStatus.Clarifying, DepthMode.Manual, InitiativeDepth.Small);
    private static readonly JourneyInput Large = new(InitiativeStatus.Clarifying, DepthMode.Manual, InitiativeDepth.Large);
    private static readonly JourneyInput Automatic = new(InitiativeStatus.Clarifying, DepthMode.Automatic, null);

    private static Conversation Answered(params string[] keys)
    {
        var conversation = Conversation.Start(Guid.NewGuid(), "user-a", T0);

        foreach (var key in keys)
        {
            Answer(conversation, key);
        }

        return conversation;
    }

    private static void Answer(Conversation conversation, string key, AnswerKind kind = AnswerKind.FreeText, string? replyKey = null)
    {
        conversation.AddAssistantMessage(key, "pregunta", [], T0);
        conversation.AddUserAnswer(key, "respuesta", kind, replyKey, false, T0);
    }

    private static JourneySnapshot Compute(JourneyInput input, Conversation conversation) =>
        ConversationJourney.Compute(input, conversation.Messages);

    /// <summary>The topics asked in turn until nothing is left, answering each one.</summary>
    private static List<string> WalkThrough(JourneyInput input, InitiativeStatus? afterConfirmation = null)
    {
        var conversation = Conversation.Start(Guid.NewGuid(), "user-a", T0);
        var asked = new List<string>();

        for (var i = 0; i < 30; i++)
        {
            var next = Compute(input, conversation).NextTopic;

            if (next is null || next.Kind == TopicKind.Closing)
            {
                break;
            }

            asked.Add(next.Key);

            if (next.Kind == TopicKind.Confirmation)
            {
                if (afterConfirmation is null)
                {
                    break;
                }

                input = input with { Status = afterConfirmation.Value };
                conversation.AddAssistantMessage(next.Key, "pregunta", [], T0);
                conversation.AddUserAnswer(next.Key, "Sí", AnswerKind.QuickReply, "confirm:yes", true, T0);
                continue;
            }

            Answer(conversation, next.Key);
        }

        return asked;
    }

    [Fact]
    public void Small_asks_six_topics_then_the_confirmation_and_no_planning_topic()
    {
        var asked = WalkThrough(Small);

        Assert.Equal(["idea", "users", "problem", "capabilities", "out-of-scope", "success", "confirm-planning"], asked);
    }

    [Fact]
    public void Standard_asks_five_topics_then_the_confirmation_then_three_planning_topics()
    {
        var asked = WalkThrough(Standard, InitiativeStatus.Planning);

        Assert.Equal(
            ["idea", "users", "problem", "success", "out-of-scope", "confirm-planning", "capabilities", "constraints", "priorities"],
            asked);
    }

    [Fact]
    public void Large_adds_integrations_and_qualities_after_priorities()
    {
        var asked = WalkThrough(Large, InitiativeStatus.Planning);

        Assert.Equal(
            [
                "idea", "users", "problem", "success", "out-of-scope", "confirm-planning",
                "capabilities", "constraints", "priorities", "integrations", "qualities",
            ],
            asked);
    }

    [Fact]
    public void Small_after_confirming_has_nothing_left_to_ask_but_the_closing()
    {
        var conversation = Answered("idea", "users", "problem", "capabilities", "out-of-scope", "success");
        conversation.AddAssistantMessage("confirm-planning", "pregunta", [], T0);
        conversation.AddUserAnswer("confirm-planning", "Sí, pasar a Planificar", AnswerKind.QuickReply, "confirm:yes", true, T0);

        var snapshot = Compute(Small with { Status = InitiativeStatus.Planning }, conversation);

        Assert.Equal(JourneyStep.Plan, snapshot.Step);
        Assert.Equal(TopicKind.Closing, snapshot.NextTopic!.Kind);
        Assert.Null(snapshot.Plan);
        Assert.False(snapshot.PlanUndefined);
    }

    [Fact]
    public void Standard_planning_ends_with_the_closing_after_the_last_topic()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope", "capabilities", "constraints", "priorities");

        var snapshot = Compute(Standard with { Status = InitiativeStatus.Planning }, conversation);

        Assert.Equal(TopicKind.Closing, snapshot.NextTopic!.Kind);
        Assert.True(snapshot.Plan!.IsComplete);
    }

    [Fact]
    public void Thin_Small_idea_is_not_redirected_and_the_next_Small_topic_follows()
    {
        var conversation = Answered("idea");

        var snapshot = Compute(Small, conversation);

        Assert.Equal("users", snapshot.NextTopic!.Key);
        Assert.Equal(new PhaseProgress(1, 6), snapshot.Clarify);
    }

    [Fact]
    public void Sizing_is_required_only_in_Automatic_without_a_level()
    {
        var all = new[] { "idea", "users", "problem", "success", "out-of-scope" };

        Assert.Equal("size", Compute(Automatic, Answered(all)).NextTopic!.Key);
        Assert.Equal("confirm-planning", Compute(Standard, Answered(all)).NextTopic!.Key);
        Assert.Equal("capabilities", Compute(Small, Answered(all)).NextTopic!.Key);
    }

    [Fact]
    public void Confirmation_is_withheld_without_a_level_and_the_proposal_follows_sizing()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope");
        Answer(conversation, "size", AnswerKind.QuickReply, "feature");

        var snapshot = Compute(Automatic, conversation);

        Assert.Equal("depth-proposal", snapshot.NextTopic!.Key);
        Assert.Equal(TopicKind.DepthProposal, snapshot.NextTopic.Kind);
    }

    [Fact]
    public void The_proposal_is_not_covered_by_an_old_answer()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope");
        Answer(conversation, "size", AnswerKind.QuickReply, "feature");
        Answer(conversation, "depth-proposal", AnswerKind.QuickReply, "depth:other");

        Assert.Equal("depth-proposal", Compute(Automatic, conversation).NextTopic!.Key);
    }

    [Fact]
    public void Free_text_on_the_sizing_choice_does_not_cover_it()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope", "size");

        Assert.Equal("size", Compute(Automatic, conversation).NextTopic!.Key);
    }

    [Fact]
    public void No_se_on_the_sizing_choice_covers_it()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope");
        Answer(conversation, "size", AnswerKind.Unknown, "unknown");

        Assert.Equal("depth-proposal", Compute(Automatic, conversation).NextTopic!.Key);
    }

    [Fact]
    public void Accepting_Small_before_capabilities_asks_it_before_the_confirmation()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope");
        Answer(conversation, "size", AnswerKind.QuickReply, "small");

        var snapshot = Compute(Small, conversation);

        Assert.Equal("capabilities", snapshot.NextTopic!.Key);
        Assert.Equal(new PhaseProgress(5, 6), snapshot.Clarify);
    }

    [Fact]
    public void Standard_to_Large_adds_only_missing_topics_after_priorities()
    {
        var conversation = Answered("idea", "users", "problem");

        Assert.Equal("success", Compute(Large, conversation).NextTopic!.Key);

        var done = Answered("idea", "users", "problem", "success", "out-of-scope", "capabilities", "constraints", "priorities");
        var snapshot = Compute(Large with { Status = InitiativeStatus.Planning }, done);

        Assert.Equal("integrations", snapshot.NextTopic!.Key);
        Assert.Equal(new PhaseProgress(8, 10), snapshot.Overall);
    }

    [Fact]
    public void Large_to_Small_drops_planning_only_answers_and_asks_the_missing_Small_topic()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope", "constraints", "priorities");

        var snapshot = Compute(Small, conversation);

        Assert.Equal("capabilities", snapshot.NextTopic!.Key);
        Assert.Equal(new PhaseProgress(5, 6), snapshot.Overall);
        Assert.Equal(7, conversation.Messages.Count(m => m.Role == MessageRole.User));
    }

    [Fact]
    public void Going_back_and_forth_between_levels_keeps_the_covered_set()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope");

        var before = Compute(Standard, conversation);
        _ = Compute(Small, conversation);
        var after = Compute(Standard, conversation);

        Assert.Equal(before, after);
        Assert.Equal(5, after.Clarify.Covered);
    }

    [Fact]
    public void Extra_answers_are_excluded_from_covered_and_total()
    {
        var conversation = Answered("idea", "users", "integrations");

        var asLarge = Compute(Large with { Status = InitiativeStatus.Planning }, conversation);
        var asStandard = Compute(Standard, conversation);

        Assert.Equal(new PhaseProgress(3, 10), asLarge.Overall);
        Assert.Equal(new PhaseProgress(2, 8), asStandard.Overall);
        Assert.Equal(3, conversation.Messages.Count(m => m.Role == MessageRole.User));
    }

    [Fact]
    public void No_se_counts_as_covered()
    {
        var conversation = Answered("idea", "users");
        Answer(conversation, "problem", AnswerKind.Unknown, "unknown");

        var snapshot = Compute(Standard, conversation);

        Assert.Equal(3, snapshot.Clarify.Covered);
        Assert.Equal("success", snapshot.NextTopic!.Key);
    }

    [Fact]
    public void Undone_answers_are_ignored()
    {
        var conversation = Answered("idea", "users", "problem");
        conversation.AddAssistantMessage("success", "pregunta", [], T0);
        conversation.UndoLastAnswer(T0);

        var snapshot = Compute(Standard, conversation);

        Assert.Equal(2, snapshot.Clarify.Covered);
        Assert.Equal("problem", snapshot.NextTopic!.Key);
    }

    [Fact]
    public void Progress_text_is_covered_out_of_the_level_total()
    {
        var snapshot = Compute(Standard, Answered("idea", "users"));

        Assert.Equal("2 de 8 temas cubiertos", snapshot.ProgressText);
        Assert.Equal(new PhaseProgress(2, 5), snapshot.Clarify);
        Assert.Equal(new PhaseProgress(0, 3), snapshot.Plan);
        Assert.Equal(3, snapshot.Clarify.Remaining);
    }

    [Fact]
    public void Small_reaches_six_of_six_before_the_confirmation()
    {
        var snapshot = Compute(Small, Answered("idea", "users", "problem", "capabilities", "out-of-scope", "success"));

        Assert.Equal("6 de 6 temas cubiertos", snapshot.ProgressText);
        Assert.Equal("confirm-planning", snapshot.NextTopic!.Key);
    }

    [Fact]
    public void Automatic_without_level_shows_no_total_and_leaves_planning_undefined()
    {
        var snapshot = Compute(Automatic, Answered("idea"));

        Assert.Null(snapshot.Overall);
        Assert.Null(snapshot.ProgressText);
        Assert.Null(snapshot.Plan);
        Assert.True(snapshot.PlanUndefined);
        Assert.Equal(new PhaseProgress(1, 6), snapshot.Clarify);
    }

    [Fact]
    public void Quick_reply_confirm_add_does_not_cover_the_confirmation()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope");
        Answer(conversation, "confirm-planning", AnswerKind.QuickReply, "confirm:add");

        var snapshot = Compute(Standard, conversation);

        Assert.Equal("confirm-planning", snapshot.NextTopic!.Key);
        Assert.False(snapshot.PendingTransition);
    }

    [Fact]
    public void Confirmed_but_still_Clarifying_is_a_pending_transition()
    {
        var conversation = Answered("idea", "users", "problem", "success", "out-of-scope");
        Answer(conversation, "confirm-planning", AnswerKind.QuickReply, "confirm:yes");

        Assert.True(Compute(Standard, conversation).PendingTransition);
        Assert.False(Compute(Standard with { Status = InitiativeStatus.Planning }, conversation).PendingTransition);
    }

    [Fact]
    public void Nothing_is_asked_for_a_draft_or_a_ready_to_build_initiative()
    {
        var conversation = Answered("idea");

        var draft = Compute(Standard with { Status = InitiativeStatus.Draft }, conversation);
        var ready = Compute(Standard with { Status = InitiativeStatus.ReadyToBuild }, conversation);

        Assert.Null(draft.NextTopic);
        Assert.Null(ready.NextTopic);
        Assert.Equal(JourneyStep.ReadyToBuild, ready.Step);
    }

    [Fact]
    public void The_step_follows_the_initiative_status()
    {
        var conversation = Answered("idea");

        Assert.Equal(JourneyStep.Clarify, Compute(Standard, conversation).Step);
        Assert.Equal(JourneyStep.Plan, Compute(Standard with { Status = InitiativeStatus.Planning }, conversation).Step);
    }

    [Fact]
    public void The_opening_topic_of_an_empty_conversation_is_the_idea()
    {
        var snapshot = Compute(Automatic, Conversation.Start(Guid.NewGuid(), "user-a", T0));

        Assert.Equal("idea", snapshot.NextTopic!.Key);
        Assert.Equal(new PhaseProgress(0, 6), snapshot.Clarify);
    }
}
