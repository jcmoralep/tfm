namespace BmadPlatform.Domain.Common;

/// <summary>
/// Raised when a domain rule is broken. The message is written in Spanish because it is shown to the user as is.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
