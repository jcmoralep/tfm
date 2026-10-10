using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BmadPlatform.Infrastructure.Assistant;

/// <summary>
/// Every read filters by owner. The conversation is returned detached with all its messages (undone included);
/// <see cref="SaveAsync"/> reapplies the changes on a fresh context. One context per call, because the callers
/// live in long-running Blazor circuits.
/// </summary>
internal sealed class ConversationRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : IConversationRepository
{
    public async Task<Conversation?> GetAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Set<Conversation>()
            .AsNoTracking()
            .Include(conversation => conversation.Messages.OrderBy(message => message.Sequence))
            .SingleOrDefaultAsync(
                conversation => conversation.InitiativeId == initiativeId && conversation.OwnerId == ownerId,
                cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Set<Conversation>().Add(conversation);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw new ConflictException();
        }
    }

    public async Task SaveAsync(Conversation conversation, int expectedVersion, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var tracked = await context.Set<Conversation>()
            .SingleOrDefaultAsync(
                stored => stored.Id == conversation.Id && stored.OwnerId == conversation.OwnerId,
                cancellationToken)
            ?? throw new NotFoundException(AssistantTexts.ConversationNotStarted);

        if (tracked.Version != expectedVersion)
        {
            throw new ConflictException();
        }

        // Ids and undo state only, untracked: the rows themselves are loaded just for the few that were undone.
        var stored = await context.Set<Message>()
            .AsNoTracking()
            .Where(message => message.ConversationId == conversation.Id)
            .Select(message => new { message.Id, IsUndone = message.UndoneAt != null })
            .ToDictionaryAsync(message => message.Id, message => message.IsUndone, cancellationToken);

        var undoneNow = new Dictionary<Guid, DateTimeOffset?>();

        foreach (var message in conversation.Messages)
        {
            if (!stored.TryGetValue(message.Id, out var isUndone))
            {
                context.Set<Message>().Add(message);
            }
            else if (message.UndoneAt is not null && !isUndone)
            {
                undoneNow[message.Id] = message.UndoneAt;
            }
        }

        if (undoneNow.Count > 0)
        {
            var ids = undoneNow.Keys.ToList();
            var toUndo = await context.Set<Message>()
                .Where(message => ids.Contains(message.Id))
                .ToListAsync(cancellationToken);

            // The only change allowed on a stored message is hiding it.
            foreach (var existing in toUndo)
            {
                context.Entry(existing).Property(nameof(Message.UndoneAt)).CurrentValue = undoneNow[existing.Id];
            }
        }

        var entry = context.Entry(tracked);
        entry.CurrentValues.SetValues(conversation);

        // The caller's version, not the entity's current one, decides whether the UPDATE finds its row.
        entry.Property(nameof(Conversation.Version)).OriginalValue = expectedVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException();
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw new ConflictException();
        }
    }
}
