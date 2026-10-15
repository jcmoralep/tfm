using RutaProducto.Domain.Assistant;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Features.Assistant.Script;

/// <summary>The initiative data the journey depends on.</summary>
public sealed record JourneyInput(InitiativeStatus Status, DepthMode? Mode, InitiativeDepth? Depth);

/// <summary>
/// Pure derivation of progress and the next topic. Nothing is stored: changing the level only changes the required
/// list, so answers are kept and an extra answer stays in history without counting.
/// </summary>
public static class ConversationJourney
{
    /// <summary>At most one model follow-up per topic, contradictions included.</summary>
    public const int MaxFollowUpsPerTopic = 1;

    /// <summary>
    /// Coverage reads the whole visible history in order (not only the answers): a question topic is covered when
    /// the last visible message with its key is a user answer, so a stored follow-up uncovers it again.
    /// </summary>
    public static JourneySnapshot Compute(JourneyInput input, IEnumerable<Message> messages)
    {
        var visible = Visible(messages);
        var answers = visible.Where(m => m.Role == MessageRole.User).ToList();

        var clarify = AssistantScript.ClarifyTopics(input.Mode, input.Depth);
        var plan = AssistantScript.PlanTopics(input.Depth);

        var clarifyProgress = Progress(clarify, visible);
        var planProgress = plan is { Count: > 0 } ? Progress(plan, visible) : null;
        var overall = plan is null
            ? null
            : new PhaseProgress(clarifyProgress.Covered + (planProgress?.Covered ?? 0), clarifyProgress.Total + (planProgress?.Total ?? 0));

        var pending = input.Status == InitiativeStatus.Clarifying
            && answers.LastOrDefault() is { TopicKey: AssistantScript.Keys.ConfirmPlanning, QuickReplyKey: AssistantScript.ReplyKeys.ConfirmYes };

        return new JourneySnapshot(
            StepFor(input.Status),
            NextTopic(input, clarify, plan, visible),
            clarifyProgress,
            planProgress,
            plan is null,
            overall,
            pending);
    }

    /// <summary>
    /// The topic whose last answer may get one follow-up: the last visible message is a free-text answer (never a
    /// "No sé" or a quick reply) to a question topic of the current required list, and the topic has no follow-up yet.
    /// The count is derived from the visible assistant messages with the key (the question itself counts as one).
    /// </summary>
    public static AssistantTopic? FollowUpCandidate(JourneyInput input, IEnumerable<Message> messages)
    {
        var visible = Visible(messages);

        if (visible.LastOrDefault() is not { Role: MessageRole.User, AnswerKind: AnswerKind.FreeText } last)
        {
            return null;
        }

        var required = input.Status switch
        {
            InitiativeStatus.Clarifying => AssistantScript.ClarifyTopics(input.Mode, input.Depth),
            InitiativeStatus.Planning => AssistantScript.PlanTopics(input.Depth) ?? [],
            _ => [],
        };

        if (!required.Contains(last.TopicKey) || AssistantScript.Get(last.TopicKey) is not { Kind: TopicKind.Question } topic)
        {
            return null;
        }

        var asked = visible.Count(m => m.Role == MessageRole.Assistant && m.TopicKey == topic.Key);

        return asked <= MaxFollowUpsPerTopic ? topic : null;
    }

    private static List<Message> Visible(IEnumerable<Message> messages) =>
        [.. messages.Where(m => m.UndoneAt is null).OrderBy(m => m.Sequence)];

    private static JourneyStep StepFor(InitiativeStatus status) => status switch
    {
        InitiativeStatus.Planning => JourneyStep.Plan,
        InitiativeStatus.ReadyToBuild => JourneyStep.ReadyToBuild,
        _ => JourneyStep.Clarify,
    };

    private static AssistantTopic? NextTopic(
        JourneyInput input,
        IReadOnlyList<string> clarify,
        IReadOnlyList<string>? plan,
        List<Message> visible)
    {
        switch (input.Status)
        {
            case InitiativeStatus.Clarifying:
                foreach (var key in clarify)
                {
                    var topic = AssistantScript.Get(key);

                    // The proposal is never covered by history: it stays until a level is set, which removes it.
                    if (topic.Kind == TopicKind.DepthProposal || (topic.CountsForProgress && !IsCovered(topic, visible)))
                    {
                        return topic;
                    }
                }

                // The confirmation is withheld until a level is set.
                return input.Depth is null ? null : AssistantScript.Get(AssistantScript.Keys.ConfirmPlanning);

            case InitiativeStatus.Planning:
                foreach (var key in plan ?? [])
                {
                    var topic = AssistantScript.Get(key);

                    if (!IsCovered(topic, visible))
                    {
                        return topic;
                    }
                }

                return AssistantScript.Get(AssistantScript.Keys.Closing);

            default:
                return null;
        }
    }

    private static PhaseProgress Progress(IReadOnlyList<string> keys, List<Message> visible)
    {
        var counted = keys.Select(AssistantScript.Get).Where(t => t.CountsForProgress).ToList();

        // The count dips by one while a follow-up is pending (the topic is uncovered again) and recovers once the
        // user answers it. This is intended: the text still reads "N de M preguntas respondidas".
        return new PhaseProgress(counted.Count(t => IsCovered(t, visible)), counted.Count);
    }

    // A question is covered when the last visible message with its key is a user answer (including "No sé"), so a
    // pending follow-up uncovers it. A choice is covered only by a quick reply, so typed text on a choice never
    // covers it. Other kinds keep the plain "any visible answer" rule.
    private static bool IsCovered(AssistantTopic topic, List<Message> visible) =>
        topic.Kind switch
        {
            TopicKind.Question => visible.LastOrDefault(m => m.TopicKey == topic.Key) is { Role: MessageRole.User },
            TopicKind.Choice => visible.Any(m => m.Role == MessageRole.User && m.TopicKey == topic.Key && m.QuickReplyKey is not null),
            _ => visible.Any(m => m.Role == MessageRole.User && m.TopicKey == topic.Key),
        };
}
