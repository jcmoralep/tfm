using System.Reflection;
using System.Text.RegularExpressions;
using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Assistant.GetConversationAnswers;
using RutaProducto.Application.Features.Assistant.Script;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Tests.Features.Assistant;

public sealed class GetConversationAnswersTests
{
    private const string Idea = AssistantScript.Keys.Idea;

    private readonly AssistantTestContext context = new();

    [Fact]
    public async Task Answers_come_back_in_script_order_with_the_latest_visible_text_per_topic()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.Start(id);
        view = await context.Send(id, view.Version, "idea v1");
        view = await context.Send(id, view.Version, "usuarios v1");
        view = await context.Undo(id, view.Version);
        view = await context.Undo(id, view.Version);
        view = await context.Send(id, view.Version, "idea v2");
        await context.Send(id, view.Version, "usuarios v2");

        var result = await context.Answers(id);

        Assert.NotNull(result);
        Assert.Equal([Idea, AssistantScript.Keys.Users], result.Answers.Select(a => a.TopicKey));
        Assert.Equal(["idea v2", "usuarios v2"], result.Answers.Select(a => a.Text));
        Assert.Equal(AssistantScript.Get(Idea).Prompt, result.Answers[0].Question);
    }

    [Theory]
    [InlineData("segunda", null, "primera\nsegunda", false)]
    [InlineData(null, AssistantScript.ReplyKeys.Unknown, "No sé", true)]
    public async Task A_topic_with_a_follow_up_gives_the_documents_both_answers(
        string? second, string? quickReplyKey, string expectedText, bool expectedUnknown)
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        context.Assistant.Verdicts.Enqueue(ReplyVerdict.NeedsMore);
        await context.Answer(id, "primera");
        await context.Answer(id, second, quickReplyKey);

        var answer = Assert.Single((await context.Answers(id))!.Answers, a => a.TopicKey == Idea);

        Assert.Equal(expectedText, answer.Text);
        Assert.Equal(expectedUnknown, answer.IsUnknown);
    }

    [Fact]
    public async Task An_undone_answer_is_excluded_and_the_aclarar_flag_falls()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.AnswerAll(id, confirm: false);
        Assert.True((await context.Answers(id))!.ClarifyComplete);

        // The newest answer is out-of-scope; success comes just before it.
        view = await context.Undo(id, view.Version);
        await context.Undo(id, view.Version);

        var result = await context.Answers(id);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Answers, a => a.TopicKey == AssistantScript.Keys.Success);
        Assert.False(result.ClarifyComplete);
    }

    [Fact]
    public async Task The_dont_know_marker_is_exposed_for_the_button_and_for_a_typed_phrase()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerAll(
            id,
            texts: new Dictionary<string, string> { [AssistantScript.Keys.Constraints] = "ni idea" },
            unknown: [AssistantScript.Keys.Problem]);

        var result = await context.Answers(id);

        Assert.NotNull(result);
        var problem = Assert.Single(result.Answers, a => a.TopicKey == AssistantScript.Keys.Problem);
        var constraints = Assert.Single(result.Answers, a => a.TopicKey == AssistantScript.Keys.Constraints);
        Assert.True(problem.IsUnknown);
        Assert.Equal(AssistantScript.UnknownLabel, problem.Text);
        Assert.True(constraints.IsUnknown);
        Assert.Equal("ni idea", constraints.Text);
        Assert.Single(result.Answers, a => a.TopicKey == AssistantScript.Keys.Capabilities && !a.IsUnknown);
    }

    [Fact]
    public async Task A_nothing_quick_reply_is_flagged_but_the_same_words_typed_are_not()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.AnswerUntilDecision(id, AssistantScript.Keys.OutOfScope);
        await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.Nothing);

        var result = await context.Answers(id);

        var outOfScope = Assert.Single(result!.Answers, a => a.TopicKey == AssistantScript.Keys.OutOfScope);
        Assert.True(outOfScope.IsNone);
        Assert.Equal("Nada por ahora", outOfScope.Text);
        Assert.All(result.Answers.Where(a => a.TopicKey != AssistantScript.Keys.OutOfScope), a => Assert.False(a.IsNone));
    }

    [Fact]
    public async Task The_aclarar_flag_is_true_with_the_planificar_flag_false_on_a_clarifying_standard_initiative()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerAll(id, confirm: false);

        var result = await context.Answers(id);

        Assert.NotNull(result);
        Assert.True(result.ClarifyComplete);
        Assert.False(result.PlanComplete);
        Assert.Equal(5, result.Answers.Count);
    }

    [Fact]
    public async Task The_closing_flag_is_true_with_all_eight_standard_topics_covered()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerAll(id);

        var result = await context.Answers(id);

        Assert.NotNull(result);
        Assert.True(result.ClarifyComplete);
        Assert.True(result.PlanComplete);
        Assert.Equal(8, result.Answers.Count);
    }

    [Fact]
    public async Task The_closing_waits_for_the_last_planificar_topic()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerAll(id);
        var view = await context.Current(id);
        Assert.Equal(AssistantScript.Keys.Closing, view.Journey.NextTopic!.Key);
        await context.Undo(id, view.Version);

        var result = await context.Answers(id);

        Assert.NotNull(result);
        Assert.True(result.ClarifyComplete);
        Assert.False(result.PlanComplete);
    }

    [Fact]
    public async Task Small_has_no_planificar_topics_so_the_closing_is_reached_when_planning_starts()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Small);
        await context.AnswerAll(id, confirm: false);

        var clarifying = await context.Answers(id);

        Assert.NotNull(clarifying);
        Assert.True(clarifying.ClarifyComplete);
        Assert.False(clarifying.PlanComplete);

        var view = await context.Current(id);
        await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);

        var planning = await context.Answers(id);

        Assert.NotNull(planning);
        Assert.True(planning.PlanComplete);
        Assert.Equal(6, planning.Answers.Count);
    }

    [Fact]
    public async Task A_ready_to_build_initiative_still_reports_its_answers_and_flags()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerAll(id);
        context.Initiatives.ForceStatus(id, InitiativeStatus.ReadyToBuild);

        var result = await context.Answers(id);

        Assert.NotNull(result);
        Assert.True(result.ClarifyComplete);
        Assert.True(result.PlanComplete);
        Assert.Equal(8, result.Answers.Count);
    }

    [Fact]
    public async Task Answers_outside_the_current_required_set_are_not_returned_and_stay_stored()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Large);
        await context.AnswerAll(id);
        Assert.Contains(
            (await context.Answers(id))!.Answers,
            a => a.TopicKey == AssistantScript.Keys.Integrations);
        var stored = context.StoredMessages(id).Count;

        typeof(Initiative).GetProperty(nameof(Initiative.Depth))!
            .SetValue(context.Initiatives.Repository.Stored.Single(i => i.Id == id), InitiativeDepth.Standard);

        var result = await context.Answers(id);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Answers, a => a.TopicKey is AssistantScript.Keys.Integrations or AssistantScript.Keys.Qualities);
        Assert.Equal(8, result.Answers.Count);
        Assert.Equal(stored, context.StoredMessages(id).Count);
    }

    [Fact]
    public async Task Choice_and_proposal_topics_are_never_returned()
    {
        var id = await context.CreateAutomatic("App");
        await context.AnswerUntilDecision(id);

        var result = await context.Answers(id);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Answers, a => a.TopicKey is AssistantScript.Keys.Size or AssistantScript.Keys.DepthProposal);
        Assert.False(result.ClarifyComplete);
    }

    [Fact]
    public async Task Without_a_conversation_there_are_no_answers_and_both_flags_are_false()
    {
        var clarifying = await context.Initiatives.CreateClarifying("App");
        var draft = await context.Initiatives.CreateDraft("Borrador");

        foreach (var id in new[] { clarifying, draft })
        {
            var result = await context.Answers(id);

            Assert.NotNull(result);
            Assert.Empty(result.Answers);
            Assert.False(result.ClarifyComplete);
            Assert.False(result.PlanComplete);
        }

        Assert.Empty(context.Conversations.Stored);
    }

    [Fact]
    public async Task Foreign_deleted_and_unknown_initiatives_read_as_null()
    {
        var foreign = await context.Initiatives.CreateClarifying("De A");
        await context.AnswerAll(foreign);
        var deleted = await context.Initiatives.CreateClarifying("Borrada");
        await context.Initiatives.Delete(deleted);

        context.User.UserId = AssistantTestContext.UserB;
        Assert.Null(await context.Answers(foreign));
        context.User.UserId = AssistantTestContext.UserA;
        Assert.Null(await context.Answers(deleted));
        Assert.Null(await context.Answers(Guid.NewGuid()));
    }

    [Fact]
    public async Task Without_a_current_user_it_fails()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        context.User.UserId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Answers(id));
    }

    [Fact]
    public async Task Reading_never_writes_or_asks_the_assistant()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        var saves = context.Conversations.SaveCount;
        var requests = context.Assistant.Requests.Count;
        var messages = context.StoredMessages(id).Count;

        await context.Answers(id);
        await context.Answers(id);

        Assert.Equal(saves, context.Conversations.SaveCount);
        Assert.Equal(requests, context.Assistant.Requests.Count);
        Assert.Equal(messages, context.StoredMessages(id).Count);
    }

    [Fact]
    public void The_dto_members_carry_no_identifiers_owner_or_timestamps()
    {
        var forbiddenName = new Regex("Id$|Owner|Mail|Timestamp|(Created|Updated|Undone)At", RegexOptions.None, TimeSpan.FromSeconds(1));

        foreach (var member in new[] { typeof(ConversationAnswers), typeof(TopicAnswer) }
                     .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public)))
        {
            Assert.DoesNotMatch(forbiddenName, member.Name);
            Assert.NotEqual(typeof(Guid), member.PropertyType);
            Assert.NotEqual(typeof(DateTimeOffset), member.PropertyType);
        }
    }

    [Fact]
    public async Task The_answer_text_is_never_logged()
    {
        const string distinctive = "texto-distintivo-8f3a1c";
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerAll(id, texts: new Dictionary<string, string> { [Idea] = distinctive });

        var result = await context.Answers(id);

        Assert.Equal(distinctive, result!.Answers.First().Text);
        Assert.DoesNotContain(context.Logs.Entries, e => e.AllText.Contains(distinctive, StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(GetConversationAnswersQueryHandler).GetConstructors().Single().GetParameters(),
            p => p.ParameterType.Name.StartsWith("ILogger", StringComparison.Ordinal));
    }
}
