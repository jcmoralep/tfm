using System.Reflection;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Domain.Assistant;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>
/// Mimics the real repository contract. Like a database it keeps its own copies: a loaded conversation is a
/// detached deep copy, so a change only becomes visible after <see cref="SaveAsync"/>, which also checks the
/// version the caller loaded. A handler that forgets to save therefore fails the test that reads
/// <see cref="Stored"/>. Another user's conversation reads as missing.
/// </summary>
public sealed class InMemoryConversationRepository : IConversationRepository
{
    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo MessagesField =
        typeof(Conversation).GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly List<Conversation> stored = [];

    /// <summary>The stored copies, not the loaded ones.</summary>
    public IReadOnlyList<Conversation> Stored => stored;

    /// <summary>Runs at the start of the next add, once, so a test can create the conversation from another tab first.</summary>
    public Func<Task>? BeforeNextAdd { get; set; }

    public int AddCount { get; private set; }

    public int SaveCount { get; private set; }

    /// <summary>Runs at the start of the next save, once, so a test can commit a competing write first.</summary>
    public Func<Task>? BeforeNextSave { get; set; }

    public Task<Conversation?> GetAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(stored
            .Where(c => c.InitiativeId == initiativeId && c.OwnerId == ownerId)
            .Select(Copy)
            .FirstOrDefault());

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        if (BeforeNextAdd is { } addHook)
        {
            BeforeNextAdd = null;
            await addHook();
        }

        if (stored.Any(c => c.InitiativeId == conversation.InitiativeId))
        {
            throw new ConflictException();
        }

        stored.Add(Copy(conversation));
        AddCount++;
    }

    public async Task SaveAsync(Conversation conversation, int expectedVersion, CancellationToken cancellationToken)
    {
        if (BeforeNextSave is { } hook)
        {
            BeforeNextSave = null;
            await hook();
        }

        var index = stored.FindIndex(c => c.Id == conversation.Id && c.OwnerId == conversation.OwnerId);

        if (index < 0)
        {
            throw new NotFoundException("La iniciativa no existe.");
        }

        if (stored[index].Version != expectedVersion)
        {
            throw new ConflictException();
        }

        stored[index] = Copy(conversation);
        SaveCount++;
    }

    // Messages are mutable (undo), so each is cloned; their quick-reply lists are never mutated and can be shared.
    private static Conversation Copy(Conversation source)
    {
        var copy = (Conversation)MemberwiseCloneMethod.Invoke(source, null)!;
        var messages = source.Messages.Select(m => (Message)MemberwiseCloneMethod.Invoke(m, null)!).ToList();
        MessagesField.SetValue(copy, messages);

        return copy;
    }
}
