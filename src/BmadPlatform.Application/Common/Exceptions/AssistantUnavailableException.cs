namespace BmadPlatform.Application.Common.Exceptions;

/// <summary>
/// Raised by an assistant service that could not produce a reply. The user's message is already stored,
/// so the conversation is left with a pending reply that the next start produces.
/// </summary>
public sealed class AssistantUnavailableException(string message) : Exception(message)
{
    public const string DefaultMessage =
        "El asistente no está disponible en este momento. Su mensaje quedó guardado; inténtelo de nuevo en unos minutos.";

    public AssistantUnavailableException()
        : this(DefaultMessage)
    {
    }
}
