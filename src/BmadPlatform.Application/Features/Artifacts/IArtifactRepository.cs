using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// Persistence port of the artifacts module. Every read receives the owner id and treats another user's document
/// exactly like a missing one. Documents are loaded as detached data: a change only becomes visible after
/// <see cref="SaveAsync"/>.
/// </summary>
public interface IArtifactRepository
{
    /// <summary>Loads one document with its content, or <c>null</c> when it was not generated.</summary>
    Task<Artifact?> GetAsync(Guid initiativeId, ArtifactType type, string ownerId, CancellationToken cancellationToken);

    /// <summary>Lists the documents of an initiative without their content, orphans included.</summary>
    Task<IReadOnlyList<ArtifactSummary>> ListAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new document. Throws <see cref="Common.Exceptions.ConflictException"/> (with the artifacts message)
    /// when the initiative already has one of that type, as happens with two tabs generating at once.
    /// </summary>
    Task AddAsync(Artifact artifact, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a document loaded with <see cref="GetAsync"/>. <paramref name="expectedVersion"/> is the version the
    /// caller loaded. Throws <see cref="Common.Exceptions.ConflictException"/> (with the artifacts message) when the
    /// stored version differs, so nothing is overwritten.
    /// </summary>
    Task SaveAsync(Artifact artifact, int expectedVersion, CancellationToken cancellationToken);
}
