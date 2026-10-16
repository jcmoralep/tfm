using RutaProducto.Application.Features.Initiatives.GetDashboard;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Features.Initiatives;

/// <summary>
/// Persistence port of the initiatives module. Every method receives the owner id and must treat
/// another user's record exactly like a missing or deleted one (returns <c>null</c> or omits it).
/// </summary>
public interface IInitiativeRepository
{
    Task<IReadOnlyList<InitiativeSummary>> ListAsync(string ownerId, InitiativeListFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// The dashboard snapshots of every non-deleted initiative owned by any of the given owners. The only method
    /// that takes several owners, so a future team view can reuse it.
    /// </summary>
    Task<IReadOnlyList<DashboardInitiative>> ListDashboardSnapshotsAsync(
        IReadOnlyCollection<string> ownerIds,
        CancellationToken cancellationToken);

    Task<InitiativeDetails?> GetDetailsAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    /// <summary>Loads the aggregate for a command.</summary>
    Task<Initiative?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    Task AddAsync(Initiative initiative, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the changes of an aggregate loaded with <see cref="GetAsync"/>.
    /// Throws <see cref="Common.Exceptions.NotFoundException"/> when the record was deleted (or removed) in the meantime,
    /// so a stale edit never brings a deleted initiative back.
    /// </summary>
    Task UpdateAsync(Initiative initiative, CancellationToken cancellationToken);
}
