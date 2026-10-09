namespace BmadPlatform.Application.Common.Exceptions;

/// <summary>
/// Raised when a write was based on a state that has changed meanwhile (a second tab, a double submit).
/// Nothing was persisted; the caller reloads and tries again.
/// </summary>
public sealed class ConflictException(string message) : Exception(message)
{
    public const string DefaultMessage = "La conversación cambió en otra pestaña. Recargue la página.";

    public ConflictException()
        : this(DefaultMessage)
    {
    }
}
