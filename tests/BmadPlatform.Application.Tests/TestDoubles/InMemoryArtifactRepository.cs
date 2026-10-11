using System.Reflection;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>
/// Mimics the real repository contract. Like a database it keeps its own copies: a loaded document is detached, so a
/// change only becomes visible after <see cref="SaveAsync"/>, which also checks the version the caller loaded. There
/// is one row per (initiative, type) and another user's document reads as missing.
/// </summary>
public sealed class InMemoryArtifactRepository : IArtifactRepository
{
    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly List<Artifact> stored = [];

    /// <summary>The stored copies, not the loaded ones.</summary>
    public IReadOnlyList<Artifact> Stored => stored;

    public int AddCount { get; private set; }

    public int SaveCount { get; private set; }

    /// <summary>Runs at the start of the next add, once, so a test can store the document from another tab first.</summary>
    public Func<Task>? BeforeNextAdd { get; set; }

    /// <summary>Runs at the start of the next save, once, so a test can commit a competing write or throw to inject a failure.</summary>
    public Func<Task>? BeforeNextSave { get; set; }

    public Task<Artifact?> GetAsync(Guid initiativeId, ArtifactType type, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(stored
            .Where(a => a.InitiativeId == initiativeId && a.Type == type && a.OwnerId == ownerId)
            .Select(Copy)
            .FirstOrDefault());

    public Task<IReadOnlyList<ArtifactSummary>> ListAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArtifactSummary> summaries = stored
            .Where(a => a.InitiativeId == initiativeId && a.OwnerId == ownerId)
            .Select(a => new ArtifactSummary(a.Type, a.Status, a.Version))
            .ToList();

        return Task.FromResult(summaries);
    }

    public async Task AddAsync(Artifact artifact, CancellationToken cancellationToken)
    {
        if (BeforeNextAdd is { } hook)
        {
            BeforeNextAdd = null;
            await hook();
        }

        if (stored.Any(a => a.InitiativeId == artifact.InitiativeId && a.Type == artifact.Type))
        {
            throw new ConflictException(ArtifactTexts.Conflict);
        }

        stored.Add(Copy(artifact));
        AddCount++;
    }

    public async Task SaveAsync(Artifact artifact, int expectedVersion, CancellationToken cancellationToken)
    {
        if (BeforeNextSave is { } hook)
        {
            BeforeNextSave = null;
            await hook();
        }

        var index = stored.FindIndex(a => a.Id == artifact.Id && a.OwnerId == artifact.OwnerId);

        if (index < 0)
        {
            throw new NotFoundException(ArtifactTexts.NotGenerated);
        }

        if (stored[index].Version != expectedVersion)
        {
            throw new ConflictException(ArtifactTexts.Conflict);
        }

        stored[index] = Copy(artifact);
        SaveCount++;
    }

    // The aggregate only holds value types and strings, so a shallow copy is a full detached copy.
    private static Artifact Copy(Artifact source) => (Artifact)MemberwiseCloneMethod.Invoke(source, null)!;
}
