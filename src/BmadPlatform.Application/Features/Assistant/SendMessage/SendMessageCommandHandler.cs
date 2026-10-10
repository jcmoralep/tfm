using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Assistant.SendMessage;

/// <summary>
/// Validates the answer, applies a depth change through Initiatives, stores the answer, then asks the advancer for
/// the assistant's turn. The depth goes first so a failed change leaves nothing stored: a stored answer that
/// changed the initiative cannot be undone, so it must never exist without its effect. A confirmation is the
/// exception: its effect (starting planning) belongs to the advancer, so the answer is stored first and a failed
/// start is completed by the next start. If the reply fails, the answer stays and the next start produces it.
/// </summary>
public sealed class SendMessageCommandHandler(
    ISender sender,
    IConversationRepository repository,
    ConversationAdvancer advancer,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<SendMessageCommand, ConversationView>
{
    public async Task<ConversationView> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ConversationGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        ConversationGuards.RequireOpen(initiative.Status);

        var conversation = await repository.GetAsync(initiative.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(AssistantTexts.ConversationNotStarted);

        if (conversation.Version != request.ExpectedVersion)
        {
            throw new ConflictException();
        }

        var journey = ConversationJourney.Compute(ConversationViewBuilder.JourneyOf(initiative), conversation.Messages);
        var answer = Resolve(request, conversation, journey);

        conversation.AddUserAnswer(
            answer.TopicKey,
            answer.Content,
            answer.Kind,
            answer.QuickReplyKey,
            answer.AppliesToInitiative,
            timeProvider.GetUtcNow());

        if (answer.Depth is { } depth)
        {
            await sender.Send(new SetInitiativeDepthCommand(initiative.Id, depth), cancellationToken);
            initiative = await ConversationGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        }

        await repository.SaveAsync(conversation, request.ExpectedVersion, cancellationToken);

        // A confirmation needs no command here: the stored answer makes the journey report a pending transition,
        // and the advancer sends StartPlanningCommand, which is also what a later resume does.
        return await advancer.AdvanceAsync(conversation, initiative, cancellationToken);
    }

    private static ResolvedAnswer Resolve(SendMessageCommand request, Conversation conversation, JourneySnapshot journey)
    {
        // While a reply is pending the last visible message is the user's own.
        if (conversation.LastVisible is not { Role: MessageRole.Assistant } question)
        {
            throw new DomainException(Conversation.ReplyPendingMessage);
        }

        QuickReply? chosen = null;

        if (!string.IsNullOrWhiteSpace(request.QuickReplyKey))
        {
            chosen = question.QuickReplies.FirstOrDefault(r => r.Key == request.QuickReplyKey)
                ?? throw new DomainException(AssistantTexts.InvalidQuickReply);
        }

        var next = journey.NextTopic;

        // A confirmation offered earlier can be stale: the level changed and topics are uncovered again.
        if (chosen?.Key == AssistantScript.ReplyKeys.ConfirmYes && next?.Key != AssistantScript.Keys.ConfirmPlanning)
        {
            throw new DomainException(AssistantTexts.IncompleteConfirmation);
        }

        // The page showed a question the journey no longer expects (level edited elsewhere): reload, do not guess.
        if (next is null || next.Key != question.TopicKey)
        {
            throw new ConflictException();
        }

        if (next.Kind == TopicKind.Closing)
        {
            throw new DomainException(AssistantTexts.NothingLeftToAnswer);
        }

        if (chosen is not null)
        {
            return new ResolvedAnswer(
                next.Key,
                chosen.Label,
                chosen.Key == AssistantScript.ReplyKeys.Unknown ? AnswerKind.Unknown : AnswerKind.QuickReply,
                chosen.Key,
                ChosenDepth(next, chosen),
                chosen.Key == AssistantScript.ReplyKeys.ConfirmYes);
        }

        var text = request.Text?.Trim() ?? string.Empty;

        // A typed "no sé" is the "No sé" button, but only on open questions: on a choice it is re-asked.
        var kind = next.Kind == TopicKind.Question && AssistantScript.IsUnknownPhrase(text) ? AnswerKind.Unknown : AnswerKind.FreeText;

        return new ResolvedAnswer(next.Key, text, kind, null, null, false);
    }

    // Only the depth proposal carries levels. "Elegir otro nivel" chooses none yet; any other key there is a mistake.
    private static InitiativeDepth? ChosenDepth(AssistantTopic topic, QuickReply chosen)
    {
        if (topic.Kind != TopicKind.DepthProposal || chosen.Key == AssistantScript.ReplyKeys.DepthOther)
        {
            return null;
        }

        return AssistantScript.ReplyKeys.TryParseDepth(chosen.Key, out var depth)
            ? depth
            : throw new DomainException(AssistantTexts.InvalidQuickReply);
    }

    private sealed record ResolvedAnswer(
        string TopicKey,
        string Content,
        AnswerKind Kind,
        string? QuickReplyKey,
        InitiativeDepth? Depth,
        bool Confirms)
    {
        // An accepted depth and a confirmation change the initiative, so they cannot be undone.
        public bool AppliesToInitiative => Depth is not null || Confirms;
    }
}
