namespace BmadPlatform.Application.Common.Exceptions;

/// <summary>
/// Raised by an assistant service or a drafter that could not produce a result. The message is shown to the person
/// as it is; <see cref="Reason"/> lets callers choose their own wording and log without content.
/// </summary>
public sealed class AssistantUnavailableException : Exception
{
    /// <summary>Neutral on purpose: it does not claim a message was saved (a document draft stores nothing).</summary>
    public const string DefaultMessage =
        "El asistente no está disponible en este momento. Inténtelo de nuevo en unos minutos.";

    public AssistantUnavailableException()
        : this(DefaultMessage)
    {
    }

    public AssistantUnavailableException(string message)
        : this(message, AssistantUnavailableReason.Timeout)
    {
    }

    public AssistantUnavailableException(string message, AssistantUnavailableReason reason, Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }

    public AssistantUnavailableReason Reason { get; }
}
