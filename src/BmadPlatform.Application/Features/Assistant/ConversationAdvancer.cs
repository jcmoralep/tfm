using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Assistant;

/// <summary>
/// The shared "apply the effect, then produce the assistant's turn" step used by start and send. It is safe to
/// run on any state: it completes a stored confirmation, then adds a message only when the last visible one is
/// not already the question the journey expects, so resuming never duplicates anything.
/// </summary>
public sealed class ConversationAdvancer(
    ISender sender,
    IAssistantService assistant,
    IConversationRepository repository,
    TimeProvider timeProvider)
{
    public async Task<ConversationView> AdvanceAsync(
        Conversation conversation,
        InitiativeDetails initiative,
        CancellationToken cancellationToken)
    {
        var journey = ConversationJourney.Compute(ConversationViewBuilder.JourneyOf(initiative), conversation.Messages);

        // The user confirmed but the initiative is still Clarifying: finish the transition (idempotent).
        if (journey.PendingTransition)
        {
            await sender.Send(new StartPlanningCommand(initiative.Id), cancellationToken);

            initiative = await sender.Send(new GetInitiativeQuery(initiative.Id), cancellationToken)
                ?? throw new NotFoundException(InitiativeTexts.NotFound);
            journey = ConversationJourney.Compute(ConversationViewBuilder.JourneyOf(initiative), conversation.Messages);
        }

        if (ConversationViewBuilder.IsOpen(initiative.Status)
            && journey.NextTopic is { } next
            && !ConversationViewBuilder.IsInSync(conversation.LastVisible, journey))
        {
            var loadedVersion = conversation.Version;

            var offersLevels = OffersLevelChoice(conversation, next);
            var reply = await assistant.ReplyAsync(BuildRequest(conversation, initiative, next, offersLevels), cancellationToken);

            conversation.AddAssistantMessage(next.Key, reply.Text, RepliesFor(next, reply, offersLevels), timeProvider.GetUtcNow());

            try
            {
                await repository.SaveAsync(conversation, loadedVersion, cancellationToken);
            }
            catch (ConflictException)
            {
                // Another tab produced the reply first: show what is stored instead of a second reply.
                conversation = await repository.GetAsync(initiative.Id, conversation.OwnerId, cancellationToken)
                    ?? throw new NotFoundException(InitiativeTexts.NotFound);
            }
        }

        return ConversationViewBuilder.Build(initiative, conversation);
    }

    private static AssistantRequest BuildRequest(
        Conversation conversation,
        InitiativeDetails initiative,
        AssistantTopic next,
        bool offersLevels) =>
        new(
            new InitiativeSnapshot(initiative.Name, initiative.Description, initiative.DepthMode, initiative.Depth),
            next,
            [.. conversation.VisibleMessages.Select(m => new ConversationTurn(m.Role, m.Content, m.TopicKey, m.QuickReplyKey, m.AnswerKind))],
            offersLevels);

    // The proposal turns into the three levels after "Elegir otro nivel", and stays that way if the user types
    // instead of picking one.
    private static bool OffersLevelChoice(Conversation conversation, AssistantTopic next)
    {
        if (next.Kind != TopicKind.DepthProposal)
        {
            return false;
        }

        var visible = conversation.VisibleMessages;

        return visible.LastOrDefault(m => m.Role == MessageRole.User)?.QuickReplyKey == AssistantScript.ReplyKeys.DepthOther
            || visible.LastOrDefault(m => m.Role == MessageRole.Assistant) is
            {
                TopicKey: AssistantScript.Keys.DepthProposal,
            } lastQuestion && lastQuestion.QuickReplies.All(r => r.Key != AssistantScript.ReplyKeys.DepthOther);
    }

    private static IReadOnlyList<QuickReply> RepliesFor(AssistantTopic topic, AssistantReply reply, bool offersLevels)
    {
        if (topic.Kind != TopicKind.DepthProposal)
        {
            return topic.QuickReplies;
        }

        return offersLevels
            ? AssistantScript.DepthLevelReplies()
            : AssistantScript.DepthProposalReplies(reply.SuggestedDepth ?? InitiativeDepth.Standard);
    }
}
