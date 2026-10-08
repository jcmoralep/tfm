using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Initiatives;

/// <summary>
/// Persistence port of the initiatives module. Every method receives the owner id and must treat
/// another user's record exactly like a missing or deleted one (returns <c>null</c> or omits it).
/// </summary>
public interface IInitiativeRepository
{
    Task<IReadOnlyList<InitiativeSummary>> ListAsync(string ownerId, InitiativeListFilter filter, CancellationToken cancellationToken);

    Task<InitiativeDetails?> GetDetailsAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    /// <summary>Loads the aggregate for a command.</summary>
    Task<Initiative?> GetAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    Task AddAsync(Initiative initiative, CancellationToken cancellationToken);

    Task UpdateAsync(Initiative initiative, CancellationToken cancellationToken);
}
