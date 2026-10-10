using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// Every read filters by owner and returns detached data. One context per call, because the callers live in
/// long-running Blazor circuits. <see cref="SaveAsync"/> reapplies the changes on a fresh context.
/// </summary>
internal sealed class ArtifactRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : IArtifactRepository
{
    public async Task<Artifact?> GetAsync(Guid initiativeId, ArtifactType type, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Set<Artifact>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                artifact => artifact.InitiativeId == initiativeId && artifact.Type == type && artifact.OwnerId == ownerId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ArtifactSummary>> ListAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Set<Artifact>()
            .AsNoTracking()
            .Where(artifact => artifact.InitiativeId == initiativeId && artifact.OwnerId == ownerId)
            .Select(artifact => new ArtifactSummary(artifact.Type, artifact.Status, artifact.Version))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Artifact artifact, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Set<Artifact>().Add(artifact);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw new ConflictException(ArtifactTexts.Conflict);
        }
    }

    public async Task SaveAsync(Artifact artifact, int expectedVersion, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var tracked = await context.Set<Artifact>()
            .SingleOrDefaultAsync(
                stored => stored.Id == artifact.Id && stored.OwnerId == artifact.OwnerId,
                cancellationToken)
            ?? throw new NotFoundException(ArtifactTexts.NotGenerated);

        if (tracked.Version != expectedVersion)
        {
            throw new ConflictException(ArtifactTexts.Conflict);
        }

        var entry = context.Entry(tracked);
        entry.CurrentValues.SetValues(artifact);

        // The caller's version, not the entity's current one, decides whether the UPDATE finds its row.
        entry.Property(nameof(Artifact.Version)).OriginalValue = expectedVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ArtifactTexts.Conflict);
        }
    }
}
