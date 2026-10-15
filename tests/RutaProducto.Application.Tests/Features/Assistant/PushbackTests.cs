using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Assistant.Script;
using RutaProducto.Domain.Assistant;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Tests.Features.Assistant;

/// <summary>One follow-up per topic through the real handlers, with a scripted verdict from the assistant double.</summary>
public sealed class PushbackTests
{
    private const string Idea = AssistantScript.Keys.Idea;
    private const string Users = AssistantScript.Keys.Users;
    private const string Problem = AssistantScript.Keys.Problem;

    private readonly AssistantTestContext context = new();

    private IEnumerable<Message> Visible(Guid id) => context.StoredMessages(id).Where(m => m.UndoneAt is null);

    private int AssistantMessagesWith(Guid id, string key) =>
        Visible(id).Count(m => m.Role == MessageRole.Assistant && m.TopicKey == key);

    private async Task<ConversationView> ThinIdea(Guid id, string text = "vago")
    {
        await context.Start(id);
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);

        return await context.Answer(id, text);
    }

    [Fact]
    public async Task A_thin_answer_gets_one_follow_up_stored_under_the_answered_key()
    {
        var id = await context.Initiatives.CreateClarifying("App");

        var view = await ThinIdea(id);

        Assert.Equal(Idea, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(MessageRole.Assistant, context.StoredMessages(id)[^1].Role);
        Assert.Equal(Idea, view.Journey.NextTopic!.Key);
        Assert.Equal(0, view.Journey.Clarify.Covered);
        Assert.True(view.CanSend);
        Assert.False(view.NeedsResume);
        Assert.Equal(Idea, context.Assistant.Requests[^1].FollowUpFor!.Key);
    }

    [Fact]
    public async Task A_sufficient_answer_proceeds_to_the_next_topic()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);

        var view = await context.Answer(id, "una idea clara");

        Assert.Equal(Users, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(1, view.Journey.Clarify.Covered);
    }

    [Fact]
    public async Task The_second_answer_is_accepted_whatever_it_says_and_a_contradiction_shares_the_cap()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.AnswerUntilDecision(id, Problem);
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);
        await context.Answer(id, "vago");
        Assert.Equal(Problem, context.StoredMessages(id)[^1].TopicKey);
        var calls = context.Assistant.Requests.Count;
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);

        view = await context.Answer(id, "se contradice");

        Assert.Equal(calls + 1, context.Assistant.Requests.Count);
        Assert.Null(context.Assistant.Requests[^1].FollowUpFor);
        Assert.Equal(AssistantScript.Keys.Success, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(3, view.Journey.Clarify.Covered);
        Assert.Equal(2, AssistantMessagesWith(id, Problem));
    }

    [Theory]
    [InlineData("ni idea", null)]
    [InlineData(null, AssistantScript.ReplyKeys.Unknown)]
    public async Task No_se_is_never_pushed_and_covers_the_topic(string? text, string? quickReplyKey)
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);

        var view = await context.Answer(id, text, quickReplyKey);

        Assert.Null(context.Assistant.Requests[^1].FollowUpFor);
        Assert.Equal(Users, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(1, view.Journey.Clarify.Covered);
        Assert.Equal(AnswerKind.Unknown, Visible(id).First(m => m.Role == MessageRole.User).AnswerKind);
    }

    [Fact]
    public async Task A_quick_reply_answer_is_never_pushed()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerUntilDecision(id, AssistantScript.Keys.OutOfScope);
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);

        var view = await context.Answer(id, quickReplyKey: AssistantScript.ReplyKeys.Nothing);

        Assert.Null(context.Assistant.Requests[^1].FollowUpFor);
        Assert.Equal(AssistantScript.Keys.ConfirmPlanning, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(5, view.Journey.Clarify.Covered);
    }

    [Fact]
    public async Task No_se_after_a_follow_up_closes_the_topic()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await ThinIdea(id);
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);

        var view = await context.Answer(id, quickReplyKey: AssistantScript.ReplyKeys.Unknown);

        Assert.Equal(Users, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(1, view.Journey.Clarify.Covered);
    }

    [Fact]
    public async Task Reloading_after_a_follow_up_asks_nothing_and_adds_nothing()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await ThinIdea(id);
        var calls = context.Assistant.Requests.Count;
        var stored = context.StoredMessages(id).Count;

        await context.Start(id);
        await context.Get(id);

        Assert.Equal(calls, context.Assistant.Requests.Count);
        Assert.Equal(stored, context.StoredMessages(id).Count);
    }

    [Fact]
    public async Task Undoing_the_original_answer_hides_its_follow_up_and_the_count_resets()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await ThinIdea(id);

        view = await context.Undo(id, view.Version);

        Assert.Single(view.Messages);
        Assert.Equal(1, AssistantMessagesWith(id, Idea));
        Assert.Equal(0, view.Journey.Clarify.Covered);

        // The hidden follow-up may be asked again.
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);
        await context.Answer(id, "otra vez vago");

        Assert.Equal(2, AssistantMessagesWith(id, Idea));
        Assert.Equal(Idea, context.StoredMessages(id)[^1].TopicKey);
    }

    [Fact]
    public async Task Undoing_the_answer_to_a_follow_up_keeps_the_follow_up_pending()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await ThinIdea(id);
        var view = await context.Answer(id, "segunda respuesta");
        Assert.Equal(1, view.Journey.Clarify.Covered);

        view = await context.Undo(id, view.Version);

        Assert.Equal(2, AssistantMessagesWith(id, Idea));
        Assert.Equal(Idea, view.Journey.NextTopic!.Key);
        Assert.Equal(0, view.Journey.Clarify.Covered);
        Assert.Equal(MessageRole.Assistant, Visible(id).Last().Role);
    }

    [Fact]
    public async Task A_thin_Small_idea_gets_one_pushback_and_no_level_redirect()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Small);

        var view = await ThinIdea(id);

        Assert.Equal(Idea, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(InitiativeDepth.Small, view.Depth);
        Assert.Equal("0 de 6 preguntas respondidas", view.Journey.ProgressText);
    }

    [Fact]
    public async Task The_progress_dips_while_a_follow_up_is_pending_and_recovers_after_the_answer()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerUntilDecision(id, Problem);
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);

        var pending = await context.Answer(id, "vago");
        var recovered = await context.Answer(id, "mejor");

        Assert.Equal("2 de 8 preguntas respondidas", pending.Journey.ProgressText);
        Assert.Equal("3 de 8 preguntas respondidas", recovered.Journey.ProgressText);
    }
}
