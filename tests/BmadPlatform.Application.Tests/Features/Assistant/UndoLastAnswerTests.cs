using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class UndoLastAnswerTests
{
    private readonly AssistantTestContext context = new();

    [Fact]
    public async Task Undo_hides_the_answer_and_its_reaction_and_the_question_is_current_again()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        await context.Answer(id, "Idea");
        await context.Answer(id, "Usuarios");
        var view = await context.Current(id);
        var requests = context.Assistant.Requests.Count;

        view = await context.Undo(id, view.Version);

        Assert.Equal([MessageRole.Assistant, MessageRole.User, MessageRole.Assistant], view.Messages.Select(m => m.Role));
        Assert.Equal(AssistantScript.Keys.Users, view.Journey.NextTopic!.Key);
        Assert.Equal(1, view.Journey.Clarify.Covered);
        Assert.True(view.CanSend);
        Assert.Equal(requests, context.Assistant.Requests.Count);
        Assert.Equal(5, context.StoredMessages(id).Count);
        Assert.Equal(2, context.StoredMessages(id).Count(m => m.UndoneAt is not null));
        Assert.Equal(context.StoredConversation(id).Version, view.Version);
    }

    [Fact]
    public async Task Repeated_undo_walks_back_until_nothing_is_left()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        await context.Answer(id, "Idea");
        await context.Answer(id, "Usuarios");

        var view = await context.Undo(id, (await context.Current(id)).Version);
        view = await context.Undo(id, view.Version);

        Assert.Single(view.Messages);
        Assert.False(view.CanUndo);
        var error = await Assert.ThrowsAsync<DomainException>(() => context.Undo(id, view.Version));
        Assert.Equal("No hay ninguna respuesta que deshacer.", error.Message);
    }

    [Fact]
    public async Task Undo_is_saved_so_the_next_load_sees_it()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        await context.Answer(id, "Idea");

        await context.Undo(id, (await context.Current(id)).Version);

        var reloaded = await context.Current(id);
        Assert.Single(reloaded.Messages);
        Assert.Contains(context.StoredMessages(id), m => m.UndoneAt is not null);
    }

    [Fact]
    public async Task Undo_with_a_pending_reply_hides_the_answer_and_asks_nothing()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);
        context.Assistant.FailuresRemaining = 1;
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Send(id, opened.Version, "Idea"));
        var requests = context.Assistant.Requests.Count;
        var pending = await context.Current(id);
        Assert.True(pending.CanUndo);

        var view = await context.Undo(id, pending.Version);

        Assert.Single(view.Messages);
        Assert.Equal(requests, context.Assistant.Requests.Count);
        Assert.False(view.NeedsResume);
        Assert.True(view.CanSend);
    }

    [Fact]
    public async Task An_answer_that_changed_the_initiative_cannot_be_undone()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        view = await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);
        Assert.False(view.CanUndo);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Undo(id, view.Version));

        Assert.Equal("No se puede deshacer una respuesta que ya cambió el estado de la iniciativa.", error.Message);
        Assert.Equal(InitiativeStatus.Planning, (await context.Details(id)).Status);
    }

    [Fact]
    public async Task An_answer_after_the_confirmation_can_be_undone_once_and_then_the_confirmation_blocks()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        view = await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);
        view = await context.Send(id, view.Version, "Buscar pedidos");

        view = await context.Undo(id, view.Version);

        Assert.Equal(AssistantScript.Keys.Capabilities, view.Journey.NextTopic!.Key);
        await Assert.ThrowsAsync<DomainException>(() => context.Undo(id, view.Version));
    }

    [Fact]
    public async Task An_accepted_depth_cannot_be_undone_and_mode_and_depth_stay()
    {
        var id = await context.CreateAutomatic("App");
        var proposal = await context.AnswerUntilDecision(id);
        var view = await context.Send(id, proposal.Version, quickReplyKey: "depth:standard");

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Undo(id, view.Version));

        Assert.Equal("No se puede deshacer una respuesta que ya cambió el estado de la iniciativa.", error.Message);
        var details = await context.Details(id);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, details.Depth);
    }

    [Fact]
    public async Task A_stale_version_is_rejected()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);
        await context.Send(id, opened.Version, "Idea");

        await Assert.ThrowsAsync<ConflictException>(() => context.Undo(id, opened.Version));

        Assert.Equal(0, context.StoredMessages(id).Count(m => m.UndoneAt is not null));
    }

    [Fact]
    public async Task Other_users_and_unknown_initiatives_are_not_found()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        var view = await context.Answer(id, "Idea");

        context.User.UserId = AssistantTestContext.UserB;
        await Assert.ThrowsAsync<NotFoundException>(() => context.Undo(id, view.Version));
        context.User.UserId = AssistantTestContext.UserA;
        await Assert.ThrowsAsync<NotFoundException>(() => context.Undo(Guid.NewGuid(), 0));

        Assert.DoesNotContain(context.StoredMessages(id), m => m.UndoneAt is not null);
    }

    [Fact]
    public async Task Draft_and_ready_to_build_reject_undo()
    {
        var draft = await context.Initiatives.CreateDraft("Borrador");
        var ready = await context.Initiatives.CreateClarifying("Lista");
        await context.Start(ready);
        var view = await context.Answer(ready, "Idea");
        context.Initiatives.ForceStatus(ready, InitiativeStatus.ReadyToBuild);

        var draftError = await Assert.ThrowsAsync<DomainException>(() => context.Undo(draft, 0));
        var readyError = await Assert.ThrowsAsync<DomainException>(() => context.Undo(ready, view.Version));

        Assert.Equal("Termine de crear la iniciativa para abrir el asistente.", draftError.Message);
        Assert.Equal("La iniciativa ya está lista para construir; la conversación es solo de lectura.", readyError.Message);
        Assert.DoesNotContain(context.StoredMessages(ready), m => m.UndoneAt is not null);
    }

    [Fact]
    public async Task Undoing_before_the_conversation_exists_is_not_found()
    {
        var id = await context.Initiatives.CreateClarifying("App");

        await Assert.ThrowsAsync<NotFoundException>(() => context.Undo(id, 0));
    }
}
