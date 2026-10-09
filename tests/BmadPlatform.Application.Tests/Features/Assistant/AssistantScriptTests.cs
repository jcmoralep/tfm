using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class AssistantScriptTests
{
    private static readonly string[] ForbiddenJargon = ["spine", "épica", "invariante", "slug"];

    private static IEnumerable<AssistantTopic> QuestionAndChoiceTopics() =>
        AssistantScript.Topics.Where(t => t.Kind is TopicKind.Question or TopicKind.Choice);

    private static int CountedTopics(InitiativeDepth depth)
    {
        var clarify = AssistantScript.ClarifyTopics(DepthMode.Manual, depth);
        var plan = AssistantScript.PlanTopics(depth)!;

        return clarify.Concat(plan).Select(AssistantScript.Get).Count(t => t.CountsForProgress);
    }

    [Theory]
    [InlineData(InitiativeDepth.Small, 6)]
    [InlineData(InitiativeDepth.Standard, 8)]
    [InlineData(InitiativeDepth.Large, 10)]
    public void Each_level_has_the_confirmed_topic_count_without_the_confirmation(InitiativeDepth depth, int expected)
    {
        Assert.Equal(expected, CountedTopics(depth));
    }

    [Fact]
    public void Small_asks_capabilities_in_Aclarar_and_has_no_Planificar_topics()
    {
        Assert.Equal(
            ["idea", "users", "problem", "capabilities", "out-of-scope", "success"],
            AssistantScript.ClarifyTopics(DepthMode.Manual, InitiativeDepth.Small));
        Assert.Empty(AssistantScript.PlanTopics(InitiativeDepth.Small)!);
        Assert.DoesNotContain("constraints", AssistantScript.ClarifyTopics(DepthMode.Manual, InitiativeDepth.Small));
    }

    [Fact]
    public void Standard_and_Large_share_the_Aclarar_list_and_differ_in_Planificar()
    {
        var expectedClarify = new[] { "idea", "users", "problem", "success", "out-of-scope" };

        Assert.Equal(expectedClarify, AssistantScript.ClarifyTopics(DepthMode.Manual, InitiativeDepth.Standard));
        Assert.Equal(expectedClarify, AssistantScript.ClarifyTopics(DepthMode.Manual, InitiativeDepth.Large));
        Assert.Equal(["capabilities", "constraints", "priorities"], AssistantScript.PlanTopics(InitiativeDepth.Standard));
        Assert.Equal(
            ["capabilities", "constraints", "priorities", "integrations", "qualities"],
            AssistantScript.PlanTopics(InitiativeDepth.Large));
    }

    [Fact]
    public void Sizing_and_proposal_are_listed_only_for_Automatic_without_level()
    {
        Assert.Equal(
            ["idea", "users", "problem", "success", "out-of-scope", "size", "depth-proposal"],
            AssistantScript.ClarifyTopics(DepthMode.Automatic, null));
        Assert.DoesNotContain("size", AssistantScript.ClarifyTopics(DepthMode.Manual, InitiativeDepth.Standard));
        Assert.DoesNotContain("size", AssistantScript.ClarifyTopics(DepthMode.Manual, InitiativeDepth.Small));
        Assert.Null(AssistantScript.PlanTopics(null));
    }

    [Fact]
    public void Every_listed_key_exists_in_the_script()
    {
        var keys = Enum.GetValues<InitiativeDepth>()
            .SelectMany(d => AssistantScript.ClarifyTopics(DepthMode.Manual, d).Concat(AssistantScript.PlanTopics(d)!))
            .Concat(AssistantScript.ClarifyTopics(DepthMode.Automatic, null));

        Assert.All(keys, key => Assert.Equal(key, AssistantScript.Get(key).Key));
    }

    [Fact]
    public void Topic_keys_are_unique_and_short_enough_to_store()
    {
        var keys = AssistantScript.Topics.Select(t => t.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, key => Assert.InRange(key.Length, 1, 50));
    }

    [Fact]
    public void Every_question_and_choice_offers_No_se_with_the_unknown_key()
    {
        Assert.All(QuestionAndChoiceTopics(), topic =>
        {
            var unknown = Assert.Single(topic.QuickReplies, r => r.Key == "unknown");
            Assert.Equal("No sé", unknown.Label);
        });
    }

    [Fact]
    public void Quick_reply_keys_are_unique_within_a_topic()
    {
        Assert.All(AssistantScript.Topics, topic =>
            Assert.Equal(topic.QuickReplies.Count, topic.QuickReplies.Select(r => r.Key).Distinct().Count()));
    }

    [Fact]
    public void Every_topic_has_a_prompt_an_example_and_a_reason()
    {
        Assert.All(AssistantScript.Topics, topic =>
        {
            Assert.False(string.IsNullOrWhiteSpace(topic.Prompt), topic.Key);
            Assert.False(string.IsNullOrWhiteSpace(topic.Example), topic.Key);
            Assert.False(string.IsNullOrWhiteSpace(topic.Why), topic.Key);
        });
    }

    [Fact]
    public void Every_topic_that_asks_something_offers_at_least_one_quick_reply()
    {
        Assert.All(
            AssistantScript.Topics.Where(t => t.Kind != TopicKind.Closing),
            topic => Assert.NotEmpty(topic.QuickReplies));
    }

    [Fact]
    public void Texts_use_no_technical_jargon()
    {
        var texts = AssistantScript.Topics
            .SelectMany(t => new[] { t.Prompt, t.Example, t.Why }.Concat(t.QuickReplies.Select(r => r.Label)))
            .Concat(AssistantScript.DepthProposalReplies(InitiativeDepth.Standard).Select(r => r.Label))
            .ToList();

        Assert.All(texts, text =>
            Assert.DoesNotContain(ForbiddenJargon, word => text.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Prompts_are_written_in_the_usted_register()
    {
        var texts = AssistantScript.Topics.SelectMany(t => new[] { t.Prompt, t.Example, t.Why });

        Assert.All(texts, text =>
        {
            Assert.DoesNotContain(" tu ", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("¿Puedes", text, StringComparison.Ordinal);
            Assert.DoesNotContain("¿Quieres", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Small_topics_never_suggest_another_level()
    {
        var smallTopics = AssistantScript.ClarifyTopics(DepthMode.Manual, InitiativeDepth.Small).Select(AssistantScript.Get);

        Assert.All(smallTopics, topic =>
        {
            Assert.DoesNotContain("nivel", topic.Prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Estándar", topic.Prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("Grande", topic.Prompt, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Confirmation_offers_exactly_the_two_replies()
    {
        var confirmation = AssistantScript.Get("confirm-planning");

        Assert.Equal(
            [("confirm:yes", "Sí, pasar a Planificar"), ("confirm:add", "Quiero añadir algo")],
            confirmation.QuickReplies.Select(r => (r.Key, r.Label)));
    }

    [Fact]
    public void Sizing_offers_the_three_sizes_and_No_se()
    {
        var size = AssistantScript.Get("size");

        Assert.Equal(TopicKind.Choice, size.Kind);
        Assert.Equal(
            ["Un ajuste pequeño", "Una funcionalidad completa", "Un producto o varias áreas", "No sé"],
            size.QuickReplies.Select(r => r.Label));
    }

    [Fact]
    public void Depth_proposal_puts_the_suggested_level_first_and_keeps_the_other_option()
    {
        var replies = AssistantScript.DepthProposalReplies(InitiativeDepth.Large);

        Assert.Equal(
            [("depth:large", "Sí, usar el nivel Grande"), ("depth:other", "Elegir otro nivel")],
            replies.Select(r => (r.Key, r.Label)));
    }

    [Fact]
    public void Choosing_another_level_shows_the_three_levels()
    {
        Assert.Equal(
            [("depth:small", "Pequeña"), ("depth:standard", "Estándar"), ("depth:large", "Grande")],
            AssistantScript.DepthLevelReplies().Select(r => (r.Key, r.Label)));
    }

    [Theory]
    [InlineData(InitiativeDepth.Small, "lista para redactar Una especificación breve")]
    [InlineData(InitiativeDepth.Standard, "lista para redactar Brief y PRD")]
    [InlineData(InitiativeDepth.Large, "lista para redactar PRD, Arquitectura y Épicas e historias")]
    public void Closing_names_the_deliverables_of_the_level(InitiativeDepth depth, string expected)
    {
        var text = AssistantScript.ClosingPrompt(depth);

        Assert.Contains(expected, text);
        Assert.DoesNotContain("{", text);
    }

    [Theory]
    [InlineData("no sé")]
    [InlineData("No sé")]
    [InlineData("NO SE")]
    [InlineData("  no se  ")]
    [InlineData("Ni idea")]
    [InlineData("ni idea.")]
    [InlineData("no lo sé")]
    [InlineData("No lo se!")]
    [InlineData("¿no sé?")]
    [InlineData("no   sé")]
    public void Whole_message_unknown_phrases_are_recognised(string text)
    {
        Assert.True(AssistantScript.IsUnknownPhrase(text));
    }

    [Theory]
    [InlineData("no sé, creo que es SAP")]
    [InlineData("quizá no sé")]
    [InlineData("no sé nada de esto")]
    [InlineData("nose")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Longer_text_or_empty_text_is_not_an_unknown_phrase(string? text)
    {
        Assert.False(AssistantScript.IsUnknownPhrase(text));
    }
}
