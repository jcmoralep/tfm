using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Assistant.GetConversation;
using RutaProducto.Application.Features.Assistant.GetConversationAnswers;
using RutaProducto.Application.Features.Assistant.Script;
using RutaProducto.Application.Features.Assistant.SendMessage;
using RutaProducto.Application.Features.Assistant.StartConversation;
using RutaProducto.Application.Features.Assistant.UndoLastAnswer;
using RutaProducto.Application.Features.Context;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Application.Tests.Features.Initiatives;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Domain.Assistant;
using RutaProducto.Domain.Initiatives;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Tests.Features.Assistant;

/// <summary>
/// Wires the real assistant handlers to the in-memory doubles and to the real Initiatives handlers (through
/// <see cref="InitiativesSender"/>). The user is the one of <see cref="Initiatives"/>.
/// </summary>
public sealed class AssistantTestContext
{
    public const string UserA = InitiativeTestContext.UserA;
    public const string UserB = InitiativeTestContext.UserB;

    public AssistantTestContext()
    {
        ContextRoutes = new ContextRoutes(
            Attachments,
            Storage,
            new ContextLimits(10L * 1024 * 1024),
            LoggerFactory.Create(builder => builder.AddProvider(Logs)));
        Sender = new InitiativesSender(Initiatives.Repository, Initiatives.User, Initiatives.Clock, ContextRoutes);
        Advancer = new ConversationAdvancer(
            Sender,
            Assistant,
            Conversations,
            Initiatives.Clock,
            LoggerFactory.Create(builder => builder.AddProvider(Logs)).CreateLogger<ConversationAdvancer>());
    }

    public CapturingLoggerProvider Logs { get; } = new();

    public InitiativeTestContext Initiatives { get; } = new();

    /// <summary>Attachments and stored files of the initiatives, read by the assistant as context.</summary>
    public InMemoryAttachmentRepository Attachments { get; } = new();

    public InMemoryFileStorage Storage { get; } = new();

    public ContextRoutes ContextRoutes { get; }

    public InMemoryConversationRepository Conversations { get; } = new();

    public ScriptedAssistantService Assistant { get; } = new();

    public InitiativesSender Sender { get; }

    public ConversationAdvancer Advancer { get; }

    public FakeCurrentUser User => Initiatives.User;

    public FixedTimeProvider Clock => Initiatives.Clock;

    public Task<ConversationView?> Get(Guid id) =>
        new GetConversationQueryHandler(Sender, Conversations, User).Handle(new GetConversationQuery(id), default);

    public Task<ConversationView> Start(Guid id) =>
        new StartConversationCommandHandler(Sender, Conversations, Advancer, User, Clock)
            .Handle(new StartConversationCommand(id), default);

    public Task<ConversationView> Send(Guid id, int expectedVersion, string? text = null, string? quickReplyKey = null) =>
        new SendMessageCommandHandler(Sender, Conversations, Advancer, User, Clock, ContextRoutes.Loggers.CreateLogger<SendMessageCommandHandler>())
            .Handle(new SendMessageCommand(id, expectedVersion, text, quickReplyKey), default);

    public Task<ConversationView> Undo(Guid id, int expectedVersion) =>
        new UndoLastAnswerCommandHandler(Sender, Conversations, User, Clock)
            .Handle(new UndoLastAnswerCommand(id, expectedVersion), default);

    /// <summary>Sends an answer on top of the stored version, as a page that just loaded would.</summary>
    public async Task<ConversationView> Answer(Guid id, string? text = null, string? quickReplyKey = null)
    {
        var view = await Get(id) ?? throw new InvalidOperationException("Initiative not found.");

        return await Send(id, view.Version, text, quickReplyKey);
    }

    public async Task<ConversationView> Current(Guid id) => await Get(id) ?? throw new InvalidOperationException("Initiative not found.");

    public Conversation StoredConversation(Guid initiativeId) => Conversations.Stored.Single(c => c.InitiativeId == initiativeId);

    public IReadOnlyList<Message> StoredMessages(Guid initiativeId) => StoredConversation(initiativeId).Messages;

    /// <summary>
    /// Starts the conversation and answers every open question (the first quick reply for a choice) until the
    /// confirmation, the depth proposal, the end, or the topic named by <paramref name="stopBeforeKey"/>.
    /// </summary>
    public async Task<ConversationView> AnswerUntilDecision(Guid id, string? stopBeforeKey = null)
    {
        var view = await Start(id);

        // Bounded so a handler that stops making progress fails the test instead of hanging it.
        for (var turn = 0; turn < 30 && view.CanSend && view.Journey.NextTopic is { } topic && topic.Key != stopBeforeKey; turn++)
        {
            switch (topic.Kind)
            {
                case TopicKind.Question:
                    view = await Send(id, view.Version, $"respuesta {topic.Key}");
                    break;
                case TopicKind.Choice:
                    view = await Send(id, view.Version, quickReplyKey: view.QuickReplies[0].Key);
                    break;
                default:
                    return view;
            }
        }

        return view;
    }

    /// <summary>
    /// Walks the script until nothing is left to ask: content topics get "respuesta {key}" unless
    /// <paramref name="texts"/> says otherwise, topics in <paramref name="unknown"/> get the "No sé" button, a choice
    /// takes its first button, and the confirmation is accepted unless <paramref name="confirm"/> is false.
    /// </summary>
    public async Task<ConversationView> AnswerAll(
        Guid id,
        IReadOnlyDictionary<string, string>? texts = null,
        IReadOnlyCollection<string>? unknown = null,
        bool confirm = true)
    {
        var view = await Start(id);

        for (var turn = 0; turn < 40 && view.CanSend && view.Journey.NextTopic is { } topic; turn++)
        {
            switch (topic.Kind)
            {
                case TopicKind.Question when unknown?.Contains(topic.Key) == true:
                    view = await Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.Unknown);
                    break;
                case TopicKind.Question:
                    view = await Send(id, view.Version, texts?.GetValueOrDefault(topic.Key) ?? $"respuesta {topic.Key}");
                    break;
                case TopicKind.Choice:
                    view = await Send(id, view.Version, quickReplyKey: view.QuickReplies[0].Key);
                    break;
                case TopicKind.Confirmation when confirm:
                    view = await Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);
                    break;
                default:
                    return view;
            }
        }

        return view;
    }

    public Task<ConversationAnswers?> Answers(Guid id) =>
        new GetConversationAnswersQueryHandler(Sender, Conversations, User).Handle(new GetConversationAnswersQuery(id), default);

    /// <summary>Creates an initiative that finished the wizard in Automatic mode, so it has no level yet.</summary>
    public async Task<Guid> CreateAutomatic(string name)
    {
        var id = await Initiatives.CreateDraft(name);
        await Initiatives.SaveDepth(id, DepthMode.Automatic, null, advance: true);
        await Initiatives.Complete(id);
        Clock.Advance(TimeSpan.FromMinutes(1));

        return id;
    }

    public async Task<InitiativeDetails> Details(Guid id) =>
        await Initiatives.Get(id) ?? throw new InvalidOperationException("Initiative not found.");
}
