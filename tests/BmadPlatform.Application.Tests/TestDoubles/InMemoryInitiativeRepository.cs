using System.Reflection;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>
/// Mimics the real repository contract: filters by owner and hides soft-deleted records, but keeps them
/// in <see cref="Stored"/> so tests can prove the record was retained. Like a database it keeps its own copies:
/// loaded aggregates are detached, so a change only becomes visible after <see cref="UpdateAsync"/>, and updating
/// a record that was deleted (or never existed) fails with <see cref="NotFoundException"/>.
/// </summary>
public sealed class InMemoryInitiativeRepository : IInitiativeRepository
{
    private readonly List<Initiative> stored = [];

    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Every record ever added, including soft-deleted ones (the stored copies, not the loaded ones).</summary>
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
            .ThenByDescending(i => i.Id)
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
        Task.FromResult(Visible(ownerId).Where(i => i.Id == id).Select(Copy).FirstOrDefault());

    public Task AddAsync(Initiative initiative, CancellationToken cancellationToken)
    {
        stored.Add(Copy(initiative));

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Initiative initiative, CancellationToken cancellationToken)
    {
        var index = stored.FindIndex(i =>
            i.Id == initiative.Id && i.CreatedByUserId == initiative.CreatedByUserId && i.DeletedAt is null);

        if (index < 0)
        {
            throw new NotFoundException(InitiativeTexts.NotFound);
        }

        stored[index] = Copy(initiative);
        UpdateCount++;

        return Task.CompletedTask;
    }

    // The aggregate only holds value types and strings, so a shallow copy is a full detached copy.
    private static Initiative Copy(Initiative source) => (Initiative)MemberwiseCloneMethod.Invoke(source, null)!;

    private IEnumerable<Initiative> Visible(string ownerId) =>
        stored.Where(i => i.CreatedByUserId == ownerId && i.DeletedAt is null);
}
