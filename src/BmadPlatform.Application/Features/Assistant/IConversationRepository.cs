using BmadPlatform.Domain.Assistant;

namespace BmadPlatform.Application.Features.Assistant;

/// <summary>
/// Persistence port of the assistant module. Every read receives the owner id and must treat another user's
/// conversation exactly like a missing one (returns <c>null</c>). The conversation is stored as a whole: the
/// aggregate is loaded with all its messages, undone ones included, as detached data.
/// </summary>
public interface IConversationRepository
{
    /// <summary>Loads the conversation of an initiative for its owner, or <c>null</c> when there is none.</summary>
    Task<Conversation?> GetAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new conversation. Throws <see cref="Common.Exceptions.ConflictException"/> when the initiative
    /// already has one (two tabs starting at once).
    /// </summary>
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the changes of a conversation loaded with <see cref="GetAsync"/>: new messages and hidden ones.
    /// <paramref name="expectedVersion"/> is the version the caller loaded. Throws
    /// <see cref="Common.Exceptions.NotFoundException"/> when the conversation no longer exists and
    /// <see cref="Common.Exceptions.ConflictException"/> when the stored version differs, so nothing is overwritten.
    /// </summary>
    Task SaveAsync(Conversation conversation, int expectedVersion, CancellationToken cancellationToken);
}
