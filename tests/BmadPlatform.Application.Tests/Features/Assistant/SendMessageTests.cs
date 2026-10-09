using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Assistant.SendMessage;
using BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using BmadPlatform.Application.Tests.TestDoubles;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class SendMessageTests
{
    private readonly AssistantTestContext context = new();

    [Fact]
    public async Task Free_text_is_stored_trimmed_and_the_next_topic_is_asked()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);

        var view = await context.Send(id, opened.Version, "  Una app de pedidos  ");

        var messages = context.StoredMessages(id);
        Assert.Equal(3, messages.Count);
        Assert.Equal(MessageRole.User, messages[1].Role);
        Assert.Equal("Una app de pedidos", messages[1].Content);
        Assert.Equal(AnswerKind.FreeText, messages[1].AnswerKind);
        Assert.Equal(AssistantScript.Keys.Idea, messages[1].TopicKey);
        Assert.False(messages[1].AppliedToInitiative);
        Assert.Equal(AssistantScript.Keys.Users, messages[2].TopicKey);
        Assert.Equal(context.StoredConversation(id).Version, view.Version);
        Assert.True(view.Version > opened.Version);
        Assert.Equal("1 de 8 temas cubiertos", view.Journey.ProgressText);
        Assert.True(view.CanSend);
        Assert.True(view.CanUndo);
    }

    [Fact]
    public async Task A_quick_reply_stores_its_label_and_key()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerUntilDecision(id, stopBeforeKey: AssistantScript.Keys.Users);
        var asked = await context.Current(id);
        Assert.Contains(asked.QuickReplies, r => r.Key == "internal");

        await context.Answer(id, quickReplyKey: "internal");

        var answer = context.StoredMessages(id).Last(m => m.Role == MessageRole.User);
        Assert.Equal("Personal interno", answer.Content);
        Assert.Equal(AnswerKind.QuickReply, answer.AnswerKind);
        Assert.Equal("internal", answer.QuickReplyKey);
        Assert.Equal(AssistantScript.Keys.Users, answer.TopicKey);
    }

    [Fact]
    public async Task The_no_se_button_is_marked_unknown_covers_the_topic_and_is_not_asked_again()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);

        var view = await context.Answer(id, quickReplyKey: AssistantScript.ReplyKeys.Unknown);

        var answer = context.StoredMessages(id).Single(m => m.Role == MessageRole.User);
        Assert.Equal(AnswerKind.Unknown, answer.AnswerKind);
        Assert.Equal("No sé", answer.Content);
        Assert.Equal(AssistantScript.Keys.Users, view.Journey.NextTopic!.Key);
        Assert.Equal(1, view.Journey.Clarify.Covered);
        Assert.Equal(AssistantScript.Keys.Users, context.Assistant.Requests.Last().NextTopic.Key);
        Assert.Equal(AnswerKind.Unknown, context.Assistant.Requests.Last().History.Last().Kind);
    }

    [Theory]
    [InlineData("No sé")]
    [InlineData("no se")]
    [InlineData("  NI IDEA  ")]
    [InlineData("No lo sé.")]
    [InlineData("¡No sé!")]
    public async Task A_typed_whole_message_no_se_counts_as_the_button(string typed)
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);

        var view = await context.Answer(id, typed);

        var answer = context.StoredMessages(id).Single(m => m.Role == MessageRole.User);
        Assert.Equal(AnswerKind.Unknown, answer.AnswerKind);
        Assert.Null(answer.QuickReplyKey);
        Assert.Equal(typed.Trim(), answer.Content);
        Assert.Equal(AssistantScript.Keys.Users, view.Journey.NextTopic!.Key);
        Assert.Equal(1, view.Journey.Clarify.Covered);
    }

    [Fact]
    public async Task Longer_text_containing_the_phrase_is_an_ordinary_answer()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);

        await context.Answer(id, "no sé, creo que es SAP");

        var answer = context.StoredMessages(id).Single(m => m.Role == MessageRole.User);
        Assert.Equal(AnswerKind.FreeText, answer.AnswerKind);
    }

    [Fact]
    public async Task Free_text_on_a_choice_topic_is_stored_and_the_same_topic_is_asked_again()
    {
        var id = await context.CreateAutomatic("App");
        var view = await context.AnswerUntilDecision(id, stopBeforeKey: AssistantScript.Keys.Size);
        Assert.Equal(AssistantScript.Keys.Size, view.Journey.NextTopic!.Key);
        var before = await context.Details(id);

        view = await context.Send(id, view.Version, "no sé");

        var stored = context.StoredMessages(id);
        Assert.Equal("no sé", stored[^2].Content);
        Assert.Equal(AnswerKind.FreeText, stored[^2].AnswerKind);
        Assert.Equal(AssistantScript.Keys.Size, stored[^1].TopicKey);
        Assert.Equal(MessageRole.Assistant, stored[^1].Role);
        Assert.Equal(AssistantScript.Keys.Size, view.Journey.NextTopic!.Key);
        Assert.Equal(before, await context.Details(id));
        Assert.Equal(AssistantScript.UnknownLabel, view.QuickReplies.Last().Label);
    }

    [Fact]
    public async Task Free_text_on_the_confirmation_is_stored_and_the_confirmation_is_offered_again()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.AnswerUntilDecision(id);
        Assert.Equal(AssistantScript.Keys.ConfirmPlanning, view.Journey.NextTopic!.Key);

        view = await context.Send(id, view.Version, "Sí, adelante");

        Assert.Equal(InitiativeStatus.Clarifying, (await context.Details(id)).Status);
        Assert.Equal(AssistantScript.Keys.ConfirmPlanning, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal([AssistantScript.ReplyKeys.ConfirmYes, AssistantScript.ReplyKeys.ConfirmAdd], view.QuickReplies.Select(r => r.Key));
    }

    [Fact]
    public async Task An_unoffered_quick_reply_is_rejected_and_nothing_is_stored()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);
        var saves = context.Conversations.SaveCount;

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Send(id, opened.Version, quickReplyKey: "customers"));

        Assert.Equal("La respuesta rápida no es válida para esta pregunta.", error.Message);
        Assert.Single(context.StoredMessages(id));
        Assert.Equal(saves, context.Conversations.SaveCount);
    }

    [Fact]
    public async Task A_stale_version_is_rejected_and_nothing_is_stored()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var tabB = await context.Start(id);
        await context.Send(id, tabB.Version, "Desde la pestaña A");
        var messages = context.StoredMessages(id).Count;

        var error = await Assert.ThrowsAsync<ConflictException>(() => context.Send(id, tabB.Version, "Desde la pestaña B"));

        Assert.Equal("La conversación cambió en otra pestaña. Recargue la página.", error.Message);
        Assert.Equal(messages, context.StoredMessages(id).Count);
    }

    [Fact]
    public async Task Two_answers_racing_for_the_same_turn_store_only_one()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);

        // The other tab commits its answer and the reply between our load and our save.
        context.Conversations.BeforeNextSave = () => context.Send(id, opened.Version, "Respuesta ganadora");

        await Assert.ThrowsAsync<ConflictException>(() => context.Send(id, opened.Version, "Respuesta perdedora"));

        var stored = context.StoredMessages(id);
        Assert.Equal([1, 2, 3], stored.Select(m => m.Sequence));
        Assert.Equal("Respuesta ganadora", Assert.Single(stored, m => m.Role == MessageRole.User).Content);
        Assert.DoesNotContain(stored, m => m.Content == "Respuesta perdedora");
    }

    [Fact]
    public async Task A_page_showing_a_question_the_journey_no_longer_expects_must_reload()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        await context.Start(id);
        await context.Answer(id, "Idea");
        await context.Answer(id, "Usuarios");
        await context.Answer(id, "Problema");
        var view = await context.Current(id);
        Assert.Equal(AssistantScript.Keys.Success, view.Journey.NextTopic!.Key);

        // Elsewhere the level is edited to Small, which asks capabilities before success.
        await context.Initiatives.Update(id, "App", null, DepthMode.Manual, InitiativeDepth.Small);
        var messages = context.StoredMessages(id).Count;

        await Assert.ThrowsAsync<ConflictException>(() => context.Send(id, view.Version, "Éxito"));

        Assert.Equal(messages, context.StoredMessages(id).Count);
    }

    [Fact]
    public async Task Text_of_exactly_2000_characters_with_surrounding_spaces_is_stored_without_them()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        var text = new string('a', 2000);

        await context.Answer(id, $"   {text}   ");

        Assert.Equal(text, context.StoredMessages(id).Single(m => m.Role == MessageRole.User).Content);
    }

    [Fact]
    public async Task Draft_and_ready_to_build_reject_sending()
    {
        var draft = await context.Initiatives.CreateDraft("Borrador");
        var ready = await context.Initiatives.CreateClarifying("Lista");
        var opened = await context.Start(ready);
        context.Initiatives.ForceStatus(ready, InitiativeStatus.ReadyToBuild);
        var messages = context.StoredMessages(ready).Count;

        var draftError = await Assert.ThrowsAsync<DomainException>(() => context.Send(draft, 0, "Hola"));
        var readyError = await Assert.ThrowsAsync<DomainException>(() => context.Send(ready, opened.Version, "Hola"));

        Assert.Equal("Termine de crear la iniciativa para abrir el asistente.", draftError.Message);
        Assert.Equal("La iniciativa ya está lista para construir; la conversación es solo de lectura.", readyError.Message);
        Assert.Equal(messages, context.StoredMessages(ready).Count);
        Assert.Single(context.Conversations.Stored);
    }

    [Fact]
    public async Task Sending_before_the_conversation_was_started_is_not_found()
    {
        var id = await context.Initiatives.CreateClarifying("App");

        await Assert.ThrowsAsync<NotFoundException>(() => context.Send(id, 0, "Hola"));

        Assert.Empty(context.Conversations.Stored);
    }

    [Fact]
    public async Task Foreign_deleted_and_unknown_initiatives_are_not_found_and_nothing_is_stored()
    {
        var foreign = await context.Initiatives.CreateClarifying("De A");
        var opened = await context.Start(foreign);
        var deleted = await context.Initiatives.CreateClarifying("Borrada");
        await context.Initiatives.Delete(deleted);
        var messages = context.StoredMessages(foreign).Count;

        context.User.UserId = AssistantTestContext.UserB;
        await Assert.ThrowsAsync<NotFoundException>(() => context.Send(foreign, opened.Version, "Intruso"));
        context.User.UserId = AssistantTestContext.UserA;
        await Assert.ThrowsAsync<NotFoundException>(() => context.Send(deleted, 0, "Hola"));
        await Assert.ThrowsAsync<NotFoundException>(() => context.Send(Guid.NewGuid(), 0, "Hola"));

        Assert.Equal(messages, context.StoredMessages(foreign).Count);
    }

    [Fact]
    public async Task Without_a_current_user_it_fails_and_nothing_is_stored()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);
        context.User.UserId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Send(id, opened.Version, "Hola"));

        Assert.Single(context.StoredMessages(id));
    }

    [Fact]
    public async Task A_failing_service_leaves_the_answer_pending_and_resuming_produces_one_reply()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);
        context.Assistant.FailuresRemaining = 1;

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Send(id, opened.Version, "Mi idea"));

        Assert.Equal(
            "El asistente no está disponible en este momento. Su mensaje quedó guardado; inténtelo de nuevo en unos minutos.",
            error.Message);
        var pending = await context.Current(id);
        Assert.Equal(MessageRole.User, context.StoredMessages(id).Last().Role);
        Assert.True(pending.NeedsResume);
        Assert.False(pending.CanSend);

        var resumed = await context.Start(id);

        var stored = context.StoredMessages(id);
        Assert.Equal([MessageRole.Assistant, MessageRole.User, MessageRole.Assistant], stored.Select(m => m.Role));
        Assert.Equal(AssistantScript.Keys.Users, stored[^1].TopicKey);
        Assert.True(resumed.CanSend);
        Assert.False(resumed.NeedsResume);
    }

    [Fact]
    public async Task Sending_while_a_reply_is_pending_is_rejected_by_the_domain()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var opened = await context.Start(id);
        context.Assistant.FailuresRemaining = 1;
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Send(id, opened.Version, "Mi idea"));
        var messages = context.StoredMessages(id).Count;

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Answer(id, "Otra cosa"));

        Assert.Equal(
            "El asistente aún debe responder a su mensaje anterior. Vuelva a abrir la conversación para reintentar.",
            error.Message);
        Assert.Equal(messages, context.StoredMessages(id).Count);
    }

    [Fact]
    public async Task Planning_accepts_answers_to_planning_topics()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);

        var after = await context.Answer(id, "Buscar pedidos");

        Assert.Equal(InitiativeStatus.Planning, after.Status);
        Assert.Equal(AssistantScript.Keys.Capabilities, context.StoredMessages(id).Last(m => m.Role == MessageRole.User).TopicKey);
        Assert.Equal(AssistantScript.Keys.Constraints, after.Journey.NextTopic!.Key);
    }

    [Fact]
    public void ToString_redacts_the_text()
    {
        var command = new SendMessageCommand(Guid.NewGuid(), 3, "texto-secreto-91", "customers");

        var text = command.ToString();

        Assert.DoesNotContain("texto-secreto-91", text, StringComparison.Ordinal);
        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
        Assert.Contains("ExpectedVersion = 3", text, StringComparison.Ordinal);
    }

    // ---- Depth proposal (Automatic mode) ----

    [Fact]
    public async Task Accepting_the_suggested_depth_switches_to_manual_and_cannot_be_undone()
    {
        var id = await context.CreateAutomatic("App");
        context.Assistant.SuggestedDepth = InitiativeDepth.Standard;
        var proposal = await context.AnswerUntilDecision(id);
        Assert.Equal(AssistantScript.Keys.DepthProposal, proposal.Journey.NextTopic!.Key);
        Assert.Equal(
            [AssistantScript.ReplyKeys.Depth(InitiativeDepth.Standard), AssistantScript.ReplyKeys.DepthOther],
            proposal.QuickReplies.Select(r => r.Key));

        var view = await context.Send(id, proposal.Version, quickReplyKey: "depth:standard");

        var details = await context.Details(id);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, details.Depth);
        var answer = context.StoredMessages(id).Last(m => m.Role == MessageRole.User);
        Assert.True(answer.AppliedToInitiative);
        Assert.Equal("Sí, usar el nivel Estándar", answer.Content);
        Assert.False(view.CanUndo);
        Assert.Equal(AssistantScript.Keys.ConfirmPlanning, view.Journey.NextTopic!.Key);
        Assert.Equal(AssistantScript.Keys.ConfirmPlanning, context.StoredMessages(id)[^1].TopicKey);
    }

    [Fact]
    public async Task Accepting_small_before_capabilities_asks_capabilities_next()
    {
        var id = await context.CreateAutomatic("App");
        context.Assistant.SuggestedDepth = InitiativeDepth.Small;
        var proposal = await context.AnswerUntilDecision(id);

        var view = await context.Send(id, proposal.Version, quickReplyKey: "depth:small");

        Assert.Equal(AssistantScript.Keys.Capabilities, view.Journey.NextTopic!.Key);
        Assert.Equal(AssistantScript.Keys.Capabilities, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(InitiativeDepth.Small, (await context.Details(id)).Depth);
    }

    [Fact]
    public async Task Rejecting_the_suggestion_keeps_automatic_and_offers_the_three_levels_then_a_choice_sets_manual()
    {
        var id = await context.CreateAutomatic("App");
        var proposal = await context.AnswerUntilDecision(id);

        var view = await context.Send(id, proposal.Version, quickReplyKey: AssistantScript.ReplyKeys.DepthOther);

        var details = await context.Details(id);
        Assert.Equal(DepthMode.Automatic, details.DepthMode);
        Assert.Null(details.Depth);
        Assert.Equal(
            ["depth:small", "depth:standard", "depth:large"],
            view.QuickReplies.Select(r => r.Key));
        Assert.Equal(["Pequeña", "Estándar", "Grande"], view.QuickReplies.Select(r => r.Label));
        Assert.True(context.Assistant.Requests.Last().OffersLevelChoice);
        Assert.True(view.CanUndo);

        view = await context.Send(id, view.Version, quickReplyKey: "depth:large");

        details = await context.Details(id);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal(InitiativeDepth.Large, details.Depth);
        Assert.False(view.CanUndo);
    }

    [Fact]
    public async Task Typing_while_the_levels_are_offered_keeps_offering_the_levels()
    {
        var id = await context.CreateAutomatic("App");
        var proposal = await context.AnswerUntilDecision(id);
        var levels = await context.Send(id, proposal.Version, quickReplyKey: AssistantScript.ReplyKeys.DepthOther);

        var view = await context.Send(id, levels.Version, "El que sea");

        Assert.Equal(["depth:small", "depth:standard", "depth:large"], view.QuickReplies.Select(r => r.Key));
        Assert.True(context.Assistant.Requests.Last().OffersLevelChoice);
        Assert.Null((await context.Details(id)).Depth);
    }

    [Fact]
    public async Task The_confirmation_is_withheld_until_a_level_is_set()
    {
        var id = await context.CreateAutomatic("App");

        var view = await context.AnswerUntilDecision(id);

        Assert.NotEqual(AssistantScript.Keys.ConfirmPlanning, view.Journey.NextTopic!.Key);
        Assert.DoesNotContain(context.StoredMessages(id), m => m.TopicKey == AssistantScript.Keys.ConfirmPlanning);
    }

    // ---- Confirmation ----

    [Fact]
    public async Task Confirming_stores_the_answer_first_then_starts_planning_and_asks_the_first_planning_topic()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        var confirmationStoredWhenPlanningStarted = false;
        context.Sender.BeforeHandle = request =>
        {
            if (request is StartPlanningCommand)
            {
                confirmationStoredWhenPlanningStarted = context.StoredMessages(id)
                    .Any(m => m.QuickReplyKey == AssistantScript.ReplyKeys.ConfirmYes && m.AppliedToInitiative);
            }
        };

        view = await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);

        Assert.True(confirmationStoredWhenPlanningStarted);
        Assert.Single(context.Sender.Sent.OfType<StartPlanningCommand>());
        var details = await context.Details(id);
        Assert.Equal(InitiativeStatus.Planning, details.Status);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, details.Depth);
        Assert.Equal(AssistantScript.Keys.Capabilities, context.StoredMessages(id)[^1].TopicKey);
        Assert.False(view.CanUndo);
        Assert.Equal(AssistantScript.ConfirmYesLabel, context.StoredMessages(id).Last(m => m.Role == MessageRole.User).Content);
    }

    [Fact]
    public async Task A_failing_start_of_planning_keeps_the_stored_confirmation_and_surfaces_the_error()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        context.Sender.BeforeHandle = request =>
        {
            if (request is StartPlanningCommand)
            {
                throw new InvalidOperationException("boom");
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes));

        Assert.Equal(InitiativeStatus.Clarifying, (await context.Details(id)).Status);
        Assert.True(context.StoredMessages(id).Last().AppliedToInitiative);
        Assert.Equal(MessageRole.User, context.StoredMessages(id).Last().Role);
    }

    [Fact]
    public async Task A_stale_confirmation_with_uncovered_topics_is_rejected_and_the_status_stays_clarifying()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);
        Assert.Equal(AssistantScript.Keys.ConfirmPlanning, view.Journey.NextTopic!.Key);

        // Small asks capabilities in Aclarar, which was never answered.
        await context.Initiatives.Update(id, "App", null, DepthMode.Manual, InitiativeDepth.Small);
        var messages = context.StoredMessages(id).Count;

        var error = await Assert.ThrowsAsync<DomainException>(
            () => context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes));

        Assert.Equal("Aún faltan preguntas por responder antes de pasar a Planificar.", error.Message);
        Assert.Equal(InitiativeStatus.Clarifying, (await context.Details(id)).Status);
        Assert.Equal(messages, context.StoredMessages(id).Count);
        Assert.Empty(context.Sender.Sent.OfType<StartPlanningCommand>());
    }

    [Fact]
    public async Task Wanting_to_add_something_keeps_clarifying_and_offers_the_confirmation_again()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Standard);
        var view = await context.AnswerUntilDecision(id);

        view = await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmAdd);

        Assert.Equal(InitiativeStatus.Clarifying, (await context.Details(id)).Status);
        Assert.Equal(AssistantScript.Keys.ConfirmPlanning, view.Journey.NextTopic!.Key);
        Assert.Equal(AssistantScript.Keys.ConfirmPlanning, context.StoredMessages(id)[^1].TopicKey);
        Assert.Equal(MessageRole.Assistant, context.StoredMessages(id)[^1].Role);
        Assert.Equal(AssistantScript.ReplyKeys.ConfirmAdd, context.Assistant.Requests.Last().History.Last().QuickReplyKey);

        view = await context.Send(id, view.Version, "Falta mencionar a bodega");

        Assert.Equal(InitiativeStatus.Clarifying, (await context.Details(id)).Status);
        Assert.Contains(context.StoredMessages(id), m => m.Content == "Falta mencionar a bodega");
        Assert.Equal(
            [AssistantScript.ReplyKeys.ConfirmYes, AssistantScript.ReplyKeys.ConfirmAdd],
            view.QuickReplies.Select(r => r.Key));
    }

    [Fact]
    public async Task Small_ends_with_the_closing_message_and_accepts_nothing_more()
    {
        var id = await context.Initiatives.CreateClarifying("App", InitiativeDepth.Small);
        var view = await context.AnswerUntilDecision(id);
        Assert.Equal(6, view.Journey.Clarify.Covered);

        view = await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);

        Assert.Equal(InitiativeStatus.Planning, view.Status);
        Assert.Equal(AssistantScript.Keys.Closing, context.StoredMessages(id)[^1].TopicKey);
        Assert.False(view.CanSend);
        Assert.Empty(view.QuickReplies);
        var messages = context.StoredMessages(id).Count;

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Send(id, view.Version, "Algo más"));

        Assert.Equal("La etapa de preguntas ya terminó; no hay nada más que responder.", error.Message);
        Assert.Equal(messages, context.StoredMessages(id).Count);
    }

    // ---- Persistence calls (a removed call must make one of these fail) ----

    [Fact]
    public async Task Both_writes_of_a_send_reach_the_store()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.Start(id);
        var saves = context.Conversations.SaveCount;

        await context.Answer(id, "Idea");

        // One save for the answer, one for the reply: reading the stored copy proves each one happened.
        Assert.Equal(saves + 2, context.Conversations.SaveCount);
        var stored = context.StoredMessages(id);
        Assert.Contains(stored, m => m.Role == MessageRole.User && m.Content == "Idea");
        Assert.Equal(AssistantScript.Keys.Users, stored[^1].TopicKey);
    }

    [Fact]
    public async Task The_depth_command_reaches_the_initiative_store()
    {
        var id = await context.CreateAutomatic("App");
        var proposal = await context.AnswerUntilDecision(id);
        var updates = context.Initiatives.Repository.UpdateCount;

        await context.Send(id, proposal.Version, quickReplyKey: "depth:standard");

        Assert.Single(context.Sender.Sent.OfType<SetInitiativeDepthCommand>());
        Assert.Equal(updates + 1, context.Initiatives.Repository.UpdateCount);
        Assert.Equal(InitiativeDepth.Standard, context.Initiatives.Repository.Stored.Single(i => i.Id == id).Depth);
    }
}
