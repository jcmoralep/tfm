using BmadPlatform.Application.Features.Context;
using BmadPlatform.Domain.Context;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>Mimics the real repository contract: every read is filtered by owner and the list is oldest first.</summary>
public sealed class InMemoryAttachmentRepository(List<string>? journal = null) : IAttachmentRepository
{
    private readonly List<Attachment> stored = [];

    public IReadOnlyList<Attachment> Stored => stored;

    /// <summary>Throws from <see cref="AddAsync"/> when set, to simulate a failed insert.</summary>
    public Exception? FailOnAdd { get; set; }

    public Task<IReadOnlyList<Attachment>> ListAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Attachment>>(
            [.. stored.Where(a => a.InitiativeId == initiativeId && a.OwnerId == ownerId).OrderBy(a => a.CreatedAt)]);

    public Task<Attachment?> GetAsync(Guid id, Guid initiativeId, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(stored.FirstOrDefault(a => a.Id == id && a.InitiativeId == initiativeId && a.OwnerId == ownerId));

    public Task<(int Count, long TotalBytes)> UsageAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken)
    {
        var items = stored.Where(a => a.InitiativeId == initiativeId && a.OwnerId == ownerId).ToList();

        return Task.FromResult((items.Count, items.Sum(a => a.SizeBytes ?? 0)));
    }

    public Task AddAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        if (FailOnAdd is not null)
        {
            throw FailOnAdd;
        }

        stored.Add(attachment);

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, string ownerId, CancellationToken cancellationToken)
    {
        journal?.Add("row:delete");

        return Task.FromResult(stored.RemoveAll(a => a.Id == id && a.OwnerId == ownerId) > 0);
    }
}
