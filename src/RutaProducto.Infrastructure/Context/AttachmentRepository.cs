using RutaProducto.Application.Features.Context;
using RutaProducto.Domain.Context;
using RutaProducto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RutaProducto.Infrastructure.Context;

/// <summary>
/// Every read filters by owner and returns detached data. One context per call, because the callers live in
/// long-running Blazor circuits.
/// </summary>
internal sealed class AttachmentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : IAttachmentRepository
{
    public async Task<IReadOnlyList<Attachment>> ListAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Set<Attachment>()
            .AsNoTracking()
            .Where(attachment => attachment.InitiativeId == initiativeId && attachment.OwnerId == ownerId)
            .OrderBy(attachment => attachment.CreatedAt)
            .ThenBy(attachment => attachment.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Attachment?> GetAsync(Guid id, Guid initiativeId, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Set<Attachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attachment => attachment.Id == id && attachment.InitiativeId == initiativeId && attachment.OwnerId == ownerId,
                cancellationToken);
    }

    public async Task<(int Count, long TotalBytes)> UsageAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var sizes = await context.Set<Attachment>()
            .AsNoTracking()
            .Where(attachment => attachment.InitiativeId == initiativeId && attachment.OwnerId == ownerId)
            .Select(attachment => attachment.SizeBytes ?? 0)
            .ToListAsync(cancellationToken);

        return (sizes.Count, sizes.Sum());
    }

    public async Task AddAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Set<Attachment>().Add(attachment);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var deleted = await context.Set<Attachment>()
            .Where(attachment => attachment.Id == id && attachment.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }
}
