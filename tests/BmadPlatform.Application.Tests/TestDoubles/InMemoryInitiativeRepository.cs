using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>
/// Mimics the real repository contract: filters by owner and hides soft-deleted records, but keeps them
/// in <see cref="Stored"/> so tests can prove the record was retained.
/// </summary>
public sealed class InMemoryInitiativeRepository : IInitiativeRepository
{
    private readonly List<Initiative> stored = [];

    /// <summary>Every record ever added, including soft-deleted ones.</summary>
    public IReadOnlyList<Initiative> Stored => stored;

    public int UpdateCount { get; private set; }

    public Task<IReadOnlyList<InitiativeSummary>> ListAsync(string ownerId, InitiativeListFilter filter, CancellationToken cancellationToken)
    {
        var search = filter.Search?.Trim();

        var query = Visible(ownerId);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(i => i.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Status is not null)
        {
            query = query.Where(i => i.Status == filter.Status);
        }

        if (filter.Depth is not null)
        {
            query = query.Where(i => i.Depth == filter.Depth);
        }

        IReadOnlyList<InitiativeSummary> result = query
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => new InitiativeSummary(i.Id, i.Name, i.Status, i.DepthMode, i.Depth, i.UpdatedAt))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<InitiativeDetails?> GetDetailsAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        var initiative = Visible(ownerId).FirstOrDefault(i => i.Id == id);

        InitiativeDetails? details = initiative is null
            ? null
            : new InitiativeDetails(
                initiative.Id,
                initiative.Name,
                initiative.Description,
                initiative.Status,
                initiative.DepthMode,
                initiative.Depth,
                initiative.CreationStep,
                initiative.CreatedAt,
                initiative.UpdatedAt);

        return Task.FromResult(details);
    }

    public Task<Initiative?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Visible(ownerId).FirstOrDefault(i => i.Id == id));

    public Task AddAsync(Initiative initiative, CancellationToken cancellationToken)
    {
        stored.Add(initiative);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Initiative initiative, CancellationToken cancellationToken)
    {
        UpdateCount++;

        return Task.CompletedTask;
    }

    private IEnumerable<Initiative> Visible(string ownerId) =>
        stored.Where(i => i.CreatedByUserId == ownerId && i.DeletedAt is null);
}
