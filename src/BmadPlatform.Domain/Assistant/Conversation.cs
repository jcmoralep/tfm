using BmadPlatform.Domain.Common;

namespace BmadPlatform.Domain.Assistant;

/// <summary>
/// The guided conversation of one initiative. It owns its messages and keeps their order. Every mutator receives
/// the current time (the aggregate never reads the clock), refreshes <see cref="UpdatedAt"/> and increments
/// <see cref="Version"/>, which lets two tabs detect that they saw different states.
/// </summary>
public sealed class Conversation
{
    public const int UserAnswerMaxLength = 2000;

    private readonly List<Message> _messages = [];

    // Required by EF Core to materialize the entity.
    private Conversation()
    {
        OwnerId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid InitiativeId { get; private set; }

    public string OwnerId { get; private set; }

    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Every message ever stored, including undone ones, in sequence order.</summary>
    public IReadOnlyList<Message> Messages => _messages;

    /// <summary>The messages the user sees: not undone, in sequence order.</summary>
    public IReadOnlyList<Message> VisibleMessages => [.. _messages.Where(m => m.UndoneAt is null).OrderBy(m => m.Sequence)];

    public Message? LastVisible => VisibleMessages.LastOrDefault();

    /// <summary>True when the last visible answer exists and did not change the initiative.</summary>
    public bool CanUndo => LastVisibleAnswer() is { AppliedToInitiative: false };

    public static Conversation Start(Guid initiativeId, string ownerId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        InitiativeId = initiativeId,
        OwnerId = ownerId,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public void AddAssistantMessage(string topicKey, string content, IReadOnlyList<QuickReply>? quickReplies, DateTimeOffset now)
    {
        _messages.Add(Message.CreateAssistant(Id, NextSequence(), topicKey, content, quickReplies ?? [], now));
        Touch(now);
    }

    /// <summary>
    /// Stores the user's answer to the current question. The text is trimmed before the length check, and the last
    /// visible message must be the assistant's question: while a reply is pending, no new answer is accepted.
    /// </summary>
    public void AddUserAnswer(
        string topicKey,
        string content,
        AnswerKind kind,
        string? quickReplyKey,
        bool appliesToInitiative,
        DateTimeOffset now)
    {
        if (LastVisible is not { Role: MessageRole.Assistant })
        {
            throw new DomainException("El asistente aún debe responder a su mensaje anterior. Vuelva a abrir la conversación para reintentar.");
        }

        var trimmed = content?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainException("La respuesta no puede estar vacía.");
        }

        if (trimmed.Length > UserAnswerMaxLength)
        {
            throw new DomainException($"La respuesta no puede superar los {UserAnswerMaxLength} caracteres.");
        }

        _messages.Add(Message.CreateUser(Id, NextSequence(), topicKey, trimmed, kind, quickReplyKey, appliesToInitiative, now));
        Touch(now);
    }

    /// <summary>
    /// Hides the last visible user answer and every visible message after it, so the question reappears.
    /// Repeating it walks back one answer per call. It stops at the opening question and at an answer that
    /// changed the initiative.
    /// </summary>
    public void UndoLastAnswer(DateTimeOffset now)
    {
        var answer = LastVisibleAnswer()
            ?? throw new DomainException("No hay ninguna respuesta que deshacer.");

        if (answer.AppliedToInitiative)
        {
            throw new DomainException("No se puede deshacer una respuesta que ya cambió el estado de la iniciativa.");
        }

        foreach (var message in _messages.Where(m => m.UndoneAt is null && m.Sequence >= answer.Sequence))
        {
            message.Undo(now);
        }

        Touch(now);
    }

    private Message? LastVisibleAnswer() => VisibleMessages.LastOrDefault(m => m.Role == MessageRole.User);

    // Counts undone rows too, so a sequence is never reused after an undo.
    private int NextSequence() => _messages.Count + 1;

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }
}
