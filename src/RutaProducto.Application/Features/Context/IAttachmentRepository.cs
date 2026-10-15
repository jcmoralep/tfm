using RutaProducto.Domain.Context;

namespace RutaProducto.Application.Features.Context;

/// <summary>
/// Persistence port of the context module. Every read receives the owner id and treats another user's item exactly
/// like a missing one. Attachments are detached data: they are added and deleted, never updated.
/// </summary>
public interface IAttachmentRepository
{
    /// <summary>Lists the items of an initiative, oldest first.</summary>
    Task<IReadOnlyList<Attachment>> ListAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken);

    /// <summary>Loads one item of the initiative, or <c>null</c> when it does not exist or is not the owner's.</summary>
    Task<Attachment?> GetAsync(Guid id, Guid initiativeId, string ownerId, CancellationToken cancellationToken);

    /// <summary>The number of items and the bytes of the files already attached to the initiative.</summary>
    Task<(int Count, long TotalBytes)> UsageAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken);

    Task AddAsync(Attachment attachment, CancellationToken cancellationToken);

    /// <summary>Deletes the row and returns whether one existed for that owner.</summary>
    Task<bool> DeleteAsync(Guid id, string ownerId, CancellationToken cancellationToken);
}
