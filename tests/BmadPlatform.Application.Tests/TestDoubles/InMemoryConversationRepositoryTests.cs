using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Assistant;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>The double is what proves persistence calls in the handler tests, so its own contract is tested.</summary>
public sealed class InMemoryConversationRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryConversationRepository repository = new();

    private async Task<Conversation> AddedAsync(Guid? initiativeId = null)
    {
        var conversation = Conversation.Start(initiativeId ?? Guid.NewGuid(), "user-a", Now);
        conversation.AddAssistantMessage("idea", "pregunta", [], Now);
        await repository.AddAsync(conversation, default);

        return conversation;
    }

    [Fact]
    public async Task A_loaded_conversation_is_detached_until_it_is_saved()
    {
        var added = await AddedAsync();
        var loaded = await repository.GetAsync(added.InitiativeId, "user-a", default);

        loaded!.AddUserAnswer("idea", "Idea", AnswerKind.FreeText, null, false, Now);

        var again = await repository.GetAsync(added.InitiativeId, "user-a", default);
        Assert.Single(again!.Messages);
        Assert.Single(repository.Stored.Single().Messages);

        await repository.SaveAsync(loaded, again.Version, default);
        Assert.Equal(2, repository.Stored.Single().Messages.Count);
    }

    [Fact]
    public async Task Hiding_a_message_in_a_loaded_copy_does_not_leak_into_the_store()
    {
        var added = await AddedAsync();
        var loaded = await repository.GetAsync(added.InitiativeId, "user-a", default);
        loaded!.AddUserAnswer("idea", "Idea", AnswerKind.FreeText, null, false, Now);
        await repository.SaveAsync(loaded, loaded.Version - 1, default);
        var second = (await repository.GetAsync(added.InitiativeId, "user-a", default))!;

        second.UndoLastAnswer(Now);

        Assert.All(repository.Stored.Single().Messages, m => Assert.Null(m.UndoneAt));
    }

    [Fact]
    public async Task A_stale_version_or_a_second_conversation_for_the_initiative_is_a_conflict()
    {
        var added = await AddedAsync();
        var loaded = (await repository.GetAsync(added.InitiativeId, "user-a", default))!;

        await Assert.ThrowsAsync<ConflictException>(() => repository.SaveAsync(loaded, loaded.Version + 5, default));
        await Assert.ThrowsAsync<ConflictException>(
            () => repository.AddAsync(Conversation.Start(added.InitiativeId, "user-a", Now), default));
    }

    [Fact]
    public async Task Another_owner_and_unknown_initiatives_read_nothing()
    {
        var added = await AddedAsync();

        Assert.Null(await repository.GetAsync(added.InitiativeId, "user-b", default));
        Assert.Null(await repository.GetAsync(Guid.NewGuid(), "user-a", default));
    }
}
