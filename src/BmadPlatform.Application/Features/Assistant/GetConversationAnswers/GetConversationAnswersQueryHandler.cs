using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Assistant.GetConversationAnswers;

public sealed class GetConversationAnswersQueryHandler(
    ISender sender,
    IConversationRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetConversationAnswersQuery, ConversationAnswers?>
{
    public async Task<ConversationAnswers?> Handle(GetConversationAnswersQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await sender.Send(new GetInitiativeQuery(request.InitiativeId), cancellationToken);

        if (initiative is null)
        {
            return null;
        }

        var conversation = await repository.GetAsync(request.InitiativeId, ownerId, cancellationToken);

        if (conversation is null)
        {
            return new ConversationAnswers(false, false, []);
        }

        var journey = ConversationJourney.Compute(ConversationViewBuilder.JourneyOf(initiative), conversation.Messages);

        var clarifyComplete = journey.Clarify.IsComplete && initiative.Depth is not null;
        var planComplete = initiative.Status is InitiativeStatus.Planning or InitiativeStatus.ReadyToBuild
            && (journey.Plan is null || journey.Plan.IsComplete);

        return new ConversationAnswers(clarifyComplete, planComplete, AnswersOf(initiative, conversation));
    }

    // Script order, one per content topic of the current required set, the newest visible answer winning.
    private static List<TopicAnswer> AnswersOf(InitiativeDetails initiative, Conversation conversation)
    {
        var keys = AssistantScript.ClarifyTopics(initiative.DepthMode, initiative.Depth)
            .Concat(AssistantScript.PlanTopics(initiative.Depth) ?? []);
        var answers = new List<TopicAnswer>();

        foreach (var key in keys)
        {
            var topic = AssistantScript.Get(key);

            if (topic.Kind != TopicKind.Question)
            {
                continue;
            }

            var latest = conversation.VisibleMessages
                .LastOrDefault(m => m.Role == MessageRole.User && m.TopicKey == key);

            if (latest is not null)
            {
                answers.Add(new TopicAnswer(key, topic.Prompt, latest.Content, latest.AnswerKind == AnswerKind.Unknown));
            }
        }

        return answers;
    }
}
