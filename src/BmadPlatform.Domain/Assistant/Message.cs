using BmadPlatform.Domain.Common;

namespace BmadPlatform.Domain.Assistant;

/// <summary>
/// One line of a conversation. Messages are created and hidden only through <see cref="Conversation"/>.
/// Undoing hides a message (<see cref="UndoneAt"/>) and never deletes it, so <see cref="Sequence"/> is never reused.
/// </summary>
public sealed class Message
{
    public const int ContentMaxLength = 4000;
    public const int KeyMaxLength = 50;

    // Required by EF Core to materialize the entity.
    private Message()
    {
        Content = string.Empty;
        TopicKey = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public int Sequence { get; private set; }

    public MessageRole Role { get; private set; }

    public string Content { get; private set; }

    /// <summary>The script topic this message asks (assistant) or answers (user).</summary>
    public string TopicKey { get; private set; }

    /// <summary>The buttons the user saw. Only assistant messages carry them.</summary>
    public IReadOnlyList<QuickReply> QuickReplies { get; private set; } = [];

    public string? QuickReplyKey { get; private set; }

    public AnswerKind? AnswerKind { get; private set; }

    /// <summary>True when this answer changed the initiative (depth accepted, planning confirmed), so it cannot be undone.</summary>
    public bool AppliedToInitiative { get; private set; }

    public DateTimeOffset? UndoneAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static Message CreateAssistant(
        Guid conversationId,
        int sequence,
        string topicKey,
        string content,
        IReadOnlyList<QuickReply> quickReplies,
        DateTimeOffset now)
    {
        RequireTopicKey(topicKey);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DomainException("El mensaje del asistente no puede estar vacío.");
        }

        if (content.Length > ContentMaxLength)
        {
            throw new DomainException($"El mensaje del asistente no puede superar los {ContentMaxLength} caracteres.");
        }

        return new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Sequence = sequence,
            Role = MessageRole.Assistant,
            Content = content,
            TopicKey = topicKey,
            QuickReplies = [.. quickReplies],
            CreatedAt = now,
        };
    }

    internal static Message CreateUser(
        Guid conversationId,
        int sequence,
        string topicKey,
        string content,
        AnswerKind kind,
        string? quickReplyKey,
        bool appliedToInitiative,
        DateTimeOffset now)
    {
        RequireTopicKey(topicKey);

        // A button answer has a key; a typed answer never has one. "No sé" may be either.
        if (kind == Assistant.AnswerKind.QuickReply && string.IsNullOrWhiteSpace(quickReplyKey))
        {
            throw new DomainException("La respuesta rápida no es válida para esta pregunta.");
        }

        if (kind == Assistant.AnswerKind.FreeText && quickReplyKey is not null)
        {
            throw new DomainException("Una respuesta escrita no puede llevar una respuesta rápida.");
        }

        if (quickReplyKey is { Length: > KeyMaxLength })
        {
            throw new DomainException("La respuesta rápida no es válida para esta pregunta.");
        }

        return new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Sequence = sequence,
            Role = MessageRole.User,
            Content = content,
            TopicKey = topicKey,
            QuickReplyKey = quickReplyKey,
            AnswerKind = kind,
            AppliedToInitiative = appliedToInitiative,
            CreatedAt = now,
        };
    }

    internal void Undo(DateTimeOffset now) => UndoneAt ??= now;

    private static void RequireTopicKey(string topicKey)
    {
        if (string.IsNullOrWhiteSpace(topicKey) || topicKey.Length > KeyMaxLength)
        {
            throw new DomainException($"La clave del tema es obligatoria y no puede superar los {KeyMaxLength} caracteres.");
        }
    }
}
