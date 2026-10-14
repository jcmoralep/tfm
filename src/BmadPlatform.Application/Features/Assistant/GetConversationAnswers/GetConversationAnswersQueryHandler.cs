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

    // Script order, one per content topic of the current required set, the visible free-text answers of a topic joined in order.
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

            var given = conversation.VisibleMessages
                .Where(m => m.Role == MessageRole.User && m.TopicKey == key)
                .ToList();

            if (given.LastOrDefault() is { } latest)
            {
                var none = latest.AnswerKind == AnswerKind.QuickReply
                    && latest.QuickReplyKey is AssistantScript.ReplyKeys.None or AssistantScript.ReplyKeys.Nothing;

                // A topic with a follow-up has two answers: the documents get both, so the first one is not lost.
                // The last answer still decides the don't-know marker, and a "No sé" keeps its own text.
                var text = latest.AnswerKind == AnswerKind.FreeText
                    ? string.Join('\n', given.Where(m => m.AnswerKind == AnswerKind.FreeText).Select(m => m.Content))
                    : latest.Content;

                answers.Add(new TopicAnswer(key, topic.Prompt, text, latest.AnswerKind == AnswerKind.Unknown, none));
            }
        }

        return answers;
    }
}
