using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BmadPlatform.Infrastructure.Initiatives;

/// <summary>
/// Every query filters by owner; deleted records are excluded by the global query filter.
/// One context per call, because the callers live in long-running Blazor circuits.
/// </summary>
internal sealed class InitiativeRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : IInitiativeRepository
{
    public async Task<IReadOnlyList<InitiativeSummary>> ListAsync(
        string ownerId,
        InitiativeListFilter filter,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Set<Initiative>().AsNoTracking().Where(initiative => initiative.CreatedByUserId == ownerId);

        var search = filter.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            var pattern = LikePattern.Contains(search);
            query = query.Where(initiative => EF.Functions.Like(initiative.Name, pattern, LikePattern.EscapeCharacter));
        }

        if (filter.Status is { } status)
        {
            query = query.Where(initiative => initiative.Status == status);
        }

        if (filter.Depth is { } depth)
        {
            query = query.Where(initiative => initiative.Depth == depth);
        }

        return await query
            .OrderByDescending(initiative => initiative.UpdatedAt)
            .Select(initiative => new InitiativeSummary(
                initiative.Id,
                initiative.Name,
                initiative.Status,
                initiative.DepthMode,
                initiative.Depth,
                initiative.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<InitiativeDetails?> GetDetailsAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Set<Initiative>()
            .AsNoTracking()
            .Where(initiative => initiative.Id == id && initiative.CreatedByUserId == ownerId)
            .Select(initiative => new InitiativeDetails(
                initiative.Id,
                initiative.Name,
                initiative.Description,
                initiative.Status,
                initiative.DepthMode,
                initiative.Depth,
                initiative.CreationStep,
                initiative.CreatedAt,
                initiative.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Initiative?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Returned detached: UpdateAsync attaches it to a fresh context.
        return await context.Set<Initiative>()
            .AsNoTracking()
            .SingleOrDefaultAsync(initiative => initiative.Id == id && initiative.CreatedByUserId == ownerId, cancellationToken);
    }

    public async Task AddAsync(Initiative initiative, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Set<Initiative>().Add(initiative);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Initiative initiative, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Set<Initiative>().Update(initiative);
        await context.SaveChangesAsync(cancellationToken);
    }
}
