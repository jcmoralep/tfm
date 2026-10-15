using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Context;
using BmadPlatform.Application.Features.Context.GetContextForAssistant;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Features.Assistant;

/// <summary>
/// The shared "apply the effect, then produce the assistant's turn" step used by start and send. It is safe to
/// run on any state: it completes a stored confirmation, then adds a message only when the last visible one is
/// not already the question the journey expects, so resuming never duplicates anything. Two tabs resuming at the
/// same time are settled by the version: one re-check before the model call skips the work when the other tab was
/// already done, and the save still keeps only one reply when both got that far.
/// </summary>
public sealed class ConversationAdvancer(
    ISender sender,
    IAssistantService assistant,
    IConversationRepository repository,
    TimeProvider timeProvider,
    ILogger<ConversationAdvancer> logger)
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

            // A cheap read before the model call: if another tab already stored a turn, show it and spend nothing.
            var current = await repository.GetAsync(initiative.Id, conversation.OwnerId, cancellationToken)
                ?? throw new NotFoundException(InitiativeTexts.NotFound);

            if (current.Version != loadedVersion)
            {
                return ConversationViewBuilder.Build(initiative, current);
            }

            var offersLevels = OffersLevelChoice(conversation, next);
            var followUpFor = ConversationJourney.FollowUpCandidate(ConversationViewBuilder.JourneyOf(initiative), conversation.Messages);
            var context = await sender.Send(new GetContextForAssistantQuery(initiative.Id), cancellationToken);
            var reply = await ReplyAsync(
                BuildRequest(conversation, initiative, next, offersLevels, followUpFor, context),
                initiative.Id,
                cancellationToken);

            // An eligible NeedsMore is a follow-up stored under the answered key (the topic becomes uncovered and stays
            // next). An ineligible one is treated as Proceed: the reply is for the next topic, with no second call.
            var isFollowUp = reply.Verdict == ReplyVerdict.NeedsMore && followUpFor is not null;
            var asked = isFollowUp ? followUpFor! : next;

            conversation.AddAssistantMessage(
                asked.Key,
                reply.Text,
                isFollowUp ? asked.QuickReplies : RepliesFor(next, reply, offersLevels),
                timeProvider.GetUtcNow());

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

    // Only the initiative id and the topic are logged: the conversation text can hold initiative information.
    private async Task<AssistantReply> ReplyAsync(AssistantRequest request, Guid initiativeId, CancellationToken cancellationToken)
    {
        try
        {
            return await assistant.ReplyAsync(request, cancellationToken);
        }
        catch (AssistantUnavailableException exception)
        {
            logger.LogWarning(
                "The assistant could not reply for initiative {InitiativeId} on topic {TopicKey} ({Reason}).",
                initiativeId,
                request.NextTopic.Key,
                exception.Reason);

            // The chat shows the reason-specific text (the answer is already stored).
            throw new AssistantUnavailableException(AssistantTexts.UnavailableFor(exception.Reason), exception.Reason, exception);
        }
    }

    private static AssistantRequest BuildRequest(
        Conversation conversation,
        InitiativeDetails initiative,
        AssistantTopic next,
        bool offersLevels,
        AssistantTopic? followUpFor,
        IReadOnlyList<ContextExcerpt> context) =>
        new(
            new InitiativeSnapshot(initiative.Name, initiative.Description, initiative.DepthMode, initiative.Depth),
            next,
            [.. conversation.VisibleMessages.Select(m => new ConversationTurn(m.Role, m.Content, m.TopicKey, m.QuickReplyKey, m.AnswerKind))],
            offersLevels,
            followUpFor,
            context);

    // The proposal turns into the three levels after "Elegir otro nivel", and stays that way if the user types
    // instead of picking one.
    private static bool OffersLevelChoice(Conversation conversation, AssistantTopic next) =>
        next.Kind == TopicKind.DepthProposal
        && (UserAskedForAnotherLevel(conversation) || LevelListIsShown(conversation));

    private static bool UserAskedForAnotherLevel(Conversation conversation) =>
        conversation.LastVisibleOf(MessageRole.User) is { QuickReplyKey: AssistantScript.ReplyKeys.DepthOther };

    // The level list is the depth question whose buttons are only levels; the first proposal also has "Elegir otro nivel".
    private static bool LevelListIsShown(Conversation conversation) =>
        conversation.LastVisibleOf(MessageRole.Assistant) is { TopicKey: AssistantScript.Keys.DepthProposal, QuickReplies: { Count: > 0 } replies }
        && replies.All(reply => AssistantScript.ReplyKeys.TryParseDepth(reply.Key, out _));

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
