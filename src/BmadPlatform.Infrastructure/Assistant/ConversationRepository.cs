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

        var storedMessages = await context.Set<Message>()
            .Where(message => message.ConversationId == conversation.Id)
            .ToDictionaryAsync(message => message.Id, cancellationToken);

        foreach (var message in conversation.Messages)
        {
            if (storedMessages.TryGetValue(message.Id, out var existing))
            {
                // The only change allowed on a stored message is hiding it.
                context.Entry(existing).Property(nameof(Message.UndoneAt)).CurrentValue = message.UndoneAt;
            }
            else
            {
                context.Set<Message>().Add(message);
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
