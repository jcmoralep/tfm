namespace BmadPlatform.Application.Common.Exceptions;

/// <summary>
/// Raised when a record does not exist for the caller. Deliberately the same outcome for a missing,
/// deleted or foreign record, so the existence of other users' data is never revealed.
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);
