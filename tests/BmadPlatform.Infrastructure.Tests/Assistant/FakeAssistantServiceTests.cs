using System.Reflection;
using System.Text.RegularExpressions;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Infrastructure.Assistant;

namespace BmadPlatform.Infrastructure.Tests.Assistant;

public sealed class FakeAssistantServiceTests
{
    private static readonly string[] Praise = ["excelente", "genial", "perfecto", "muy bien", "buena respuesta", "gran idea", "fantástic"];

    private readonly FakeAssistantService service = new();

    private static AssistantRequest Request(
        string topicKey,
        IReadOnlyList<ConversationTurn>? history = null,
        InitiativeDepth? depth = InitiativeDepth.Standard,
        bool offersLevels = false) =>
        new(
            new InitiativeSnapshot("App de pedidos", "Consultar pedidos", DepthMode.Manual, depth),
            AssistantScript.Get(topicKey),
            history ?? [],
            offersLevels);

    private static ConversationTurn Assistant(string topicKey) =>
        new(MessageRole.Assistant, "pregunta", topicKey, null, null);

    private static ConversationTurn User(string topicKey, string text, AnswerKind kind, string? key = null) =>
        new(MessageRole.User, text, topicKey, key, kind);

    [Fact]
    public async Task Equal_requests_give_equal_replies()
    {
        IReadOnlyList<ConversationTurn> Build() =>
        [
            Assistant(AssistantScript.Keys.Idea),
            User(AssistantScript.Keys.Idea, "Una app", AnswerKind.FreeText),
        ];

        var first = await service.ReplyAsync(Request(AssistantScript.Keys.Users, Build()), default);
        var second = await new FakeAssistantService().ReplyAsync(Request(AssistantScript.Keys.Users, Build()), default);

        Assert.Equal(first, second);
    }

    [Fact]
    public void The_request_carries_no_identifiers_owner_email_or_timestamps()
    {
        var types = new[] { typeof(AssistantRequest), typeof(InitiativeSnapshot), typeof(ConversationTurn), typeof(AssistantReply) };
        var forbiddenName = new Regex("Id$|Owner|Mail|Timestamp|(Created|Updated|Undone)At", RegexOptions.None, TimeSpan.FromSeconds(1));

        foreach (var member in types.SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public)))
        {
            Assert.DoesNotMatch(forbiddenName, member.Name);
            Assert.NotEqual(typeof(Guid), member.PropertyType);
            Assert.NotEqual(typeof(DateTimeOffset), Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType);
            Assert.NotEqual(typeof(DateTime), Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType);
        }
    }

    [Theory]
    [InlineData(AssistantScript.Keys.Idea)]
    [InlineData(AssistantScript.Keys.Users)]
    [InlineData(AssistantScript.Keys.Problem)]
    [InlineData(AssistantScript.Keys.Success)]
    [InlineData(AssistantScript.Keys.OutOfScope)]
    [InlineData(AssistantScript.Keys.Capabilities)]
    [InlineData(AssistantScript.Keys.Constraints)]
    [InlineData(AssistantScript.Keys.Priorities)]
    [InlineData(AssistantScript.Keys.Integrations)]
    [InlineData(AssistantScript.Keys.Qualities)]
    [InlineData(AssistantScript.Keys.Size)]
    [InlineData(AssistantScript.Keys.DepthProposal)]
    [InlineData(AssistantScript.Keys.ConfirmPlanning)]
    public async Task Every_asked_topic_has_its_question_an_example_and_a_reason(string key)
    {
        var reply = await service.ReplyAsync(Request(key), default);

        Assert.Contains("Por ejemplo:", reply.Text, StringComparison.Ordinal);
        Assert.Contains("Por qué lo pregunto:", reply.Text, StringComparison.Ordinal);
        Assert.Contains(AssistantScript.Get(key).Why, reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_the_next_topic_is_asked()
    {
        var reply = await service.ReplyAsync(Request(AssistantScript.Keys.Users), default);

        Assert.Contains("¿Quién va a usar esto?", reply.Text, StringComparison.Ordinal);

        foreach (var other in AssistantScript.Topics.Where(t => t.Kind == TopicKind.Question && t.Key != AssistantScript.Keys.Users))
        {
            Assert.DoesNotContain(other.Prompt, reply.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_suggested_depth_is_set_only_for_the_depth_proposal()
    {
        var question = await service.ReplyAsync(Request(AssistantScript.Keys.Users), default);
        var confirmation = await service.ReplyAsync(Request(AssistantScript.Keys.ConfirmPlanning), default);

        Assert.Null(question.SuggestedDepth);
        Assert.Null(confirmation.SuggestedDepth);
    }

    [Theory]
    [InlineData(AssistantScript.ReplyKeys.SizeSmall, InitiativeDepth.Small, "Pequeña", "Una especificación breve")]
    [InlineData(AssistantScript.ReplyKeys.SizeFeature, InitiativeDepth.Standard, "Estándar", "Brief y PRD")]
    [InlineData(AssistantScript.ReplyKeys.SizeProduct, InitiativeDepth.Large, "Grande", "PRD, Arquitectura y Épicas e historias")]
    [InlineData(AssistantScript.ReplyKeys.Unknown, InitiativeDepth.Standard, "Estándar", "Brief y PRD")]
    public async Task The_suggestion_follows_the_size_answer(string sizeKey, InitiativeDepth expected, string levelName, string deliverables)
    {
        var history = new[]
        {
            Assistant(AssistantScript.Keys.Size),
            User(AssistantScript.Keys.Size, "respuesta", AnswerKind.QuickReply, sizeKey),
        };

        var reply = await service.ReplyAsync(Request(AssistantScript.Keys.DepthProposal, history, depth: null), default);

        Assert.Equal(expected, reply.SuggestedDepth);
        Assert.Contains($"le propongo el nivel {levelName}", reply.Text, StringComparison.Ordinal);
        Assert.Contains(deliverables, reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Offering_the_levels_lists_all_three_and_suggests_none()
    {
        var reply = await service.ReplyAsync(Request(AssistantScript.Keys.DepthProposal, depth: null, offersLevels: true), default);

        Assert.Null(reply.SuggestedDepth);
        Assert.Contains(AssistantTexts.ChooseLevelPrompt, reply.Text, StringComparison.Ordinal);
        Assert.Contains("Pequeña", reply.Text, StringComparison.Ordinal);
        Assert.Contains("Estándar", reply.Text, StringComparison.Ordinal);
        Assert.Contains("Grande", reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_closing_says_ready_to_draft_with_the_deliverables_of_the_level()
    {
        var reply = await service.ReplyAsync(Request(AssistantScript.Keys.Closing, depth: InitiativeDepth.Small), default);

        Assert.Contains("lista para redactar", reply.Text, StringComparison.Ordinal);
        Assert.Contains("Una especificación breve", reply.Text, StringComparison.Ordinal);
        Assert.Contains("siguiente paso es generar los documentos", reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_acknowledgement_depends_on_the_last_answer()
    {
        var opening = await service.ReplyAsync(Request(AssistantScript.Keys.Idea), default);
        var plain = await service.ReplyAsync(
            Request(AssistantScript.Keys.Users, [Assistant("idea"), User("idea", "Una app", AnswerKind.FreeText)]),
            default);
        var unknown = await service.ReplyAsync(
            Request(AssistantScript.Keys.Users, [Assistant("idea"), User("idea", "No sé", AnswerKind.Unknown, "unknown")]),
            default);
        var add = await service.ReplyAsync(
            Request(
                AssistantScript.Keys.ConfirmPlanning,
                [Assistant("confirm-planning"), User("confirm-planning", "Quiero añadir algo", AnswerKind.QuickReply, "confirm:add")]),
            default);
        var reAsk = await service.ReplyAsync(
            Request(
                AssistantScript.Keys.Size,
                [Assistant("size"), User("size", "no sé", AnswerKind.FreeText)]),
            default);
        var addition = await service.ReplyAsync(
            Request(
                AssistantScript.Keys.ConfirmPlanning,
                [
                    Assistant("confirm-planning"),
                    User("confirm-planning", "Quiero añadir algo", AnswerKind.QuickReply, "confirm:add"),
                    Assistant("confirm-planning"),
                    User("confirm-planning", "Falta bodega", AnswerKind.FreeText),
                ]),
            default);

        Assert.StartsWith("Cuénteme la idea", opening.Text, StringComparison.Ordinal);
        Assert.StartsWith("Anotado. ¿Quién", plain.Text, StringComparison.Ordinal);
        Assert.StartsWith("No pasa nada: lo dejo anotado como pregunta pendiente y seguimos. ¿Quién", unknown.Text, StringComparison.Ordinal);
        Assert.StartsWith("Claro, escriba lo que quiera añadir. ", add.Text, StringComparison.Ordinal);
        Assert.StartsWith("Para seguir, elija una de las opciones. ", reAsk.Text, StringComparison.Ordinal);
        Assert.StartsWith("Anotado. ", addition.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_reply_praises_the_user()
    {
        var histories = new IReadOnlyList<ConversationTurn>[]
        {
            [],
            [Assistant("idea"), User("idea", "Una app", AnswerKind.FreeText)],
            [Assistant("idea"), User("idea", "No sé", AnswerKind.Unknown, "unknown")],
            [Assistant("size"), User("size", "texto", AnswerKind.FreeText)],
        };

        foreach (var topic in AssistantScript.Topics)
        {
            foreach (var history in histories)
            {
                var reply = await service.ReplyAsync(Request(topic.Key, history), default);

                foreach (var word in Praise)
                {
                    Assert.DoesNotContain(word, reply.Text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public async Task A_cancelled_token_is_honoured()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ReplyAsync(Request(AssistantScript.Keys.Idea), source.Token));
    }
}
