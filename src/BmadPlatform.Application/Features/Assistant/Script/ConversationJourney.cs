using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Assistant.Script;

/// <summary>The initiative data the journey depends on.</summary>
public sealed record JourneyInput(InitiativeStatus Status, DepthMode? Mode, InitiativeDepth? Depth);

/// <summary>
/// Pure derivation of progress and the next topic. Nothing is stored: changing the level only changes the required
/// list, so answers are kept and an extra answer stays in history without counting.
/// </summary>
public static class ConversationJourney
{
    public static JourneySnapshot Compute(JourneyInput input, IEnumerable<Message> messages)
    {
        var answers = messages
            .Where(m => m.Role == MessageRole.User && m.UndoneAt is null)
            .OrderBy(m => m.Sequence)
            .ToList();

        var clarify = AssistantScript.ClarifyTopics(input.Mode, input.Depth);
        var plan = AssistantScript.PlanTopics(input.Depth);

        var clarifyProgress = Progress(clarify, answers);
        var planProgress = plan is { Count: > 0 } ? Progress(plan, answers) : null;
        var overall = plan is null
            ? null
            : new PhaseProgress(clarifyProgress.Covered + (planProgress?.Covered ?? 0), clarifyProgress.Total + (planProgress?.Total ?? 0));

        var pending = input.Status == InitiativeStatus.Clarifying
            && answers.LastOrDefault() is { TopicKey: AssistantScript.Keys.ConfirmPlanning, QuickReplyKey: AssistantScript.ReplyKeys.ConfirmYes };

        return new JourneySnapshot(
            StepFor(input.Status),
            NextTopic(input, clarify, plan, answers),
            clarifyProgress,
            planProgress,
            plan is null,
            overall,
            pending);
    }

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
        List<Message> answers)
    {
        switch (input.Status)
        {
            case InitiativeStatus.Clarifying:
                foreach (var key in clarify)
                {
                    var topic = AssistantScript.Get(key);

                    // The proposal is never covered by history: it stays until a level is set, which removes it.
                    if (topic.Kind == TopicKind.DepthProposal || (topic.CountsForProgress && !IsCovered(topic, answers)))
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

                    if (!IsCovered(topic, answers))
                    {
                        return topic;
                    }
                }

                return AssistantScript.Get(AssistantScript.Keys.Closing);

            default:
                return null;
        }
    }

    private static PhaseProgress Progress(IReadOnlyList<string> keys, List<Message> answers)
    {
        var counted = keys.Select(AssistantScript.Get).Where(t => t.CountsForProgress).ToList();

        return new PhaseProgress(counted.Count(t => IsCovered(t, answers)), counted.Count);
    }

    // A question is covered by any visible answer with its key (including "No sé"). A choice only by a quick
    // reply, so typed text on a choice never covers it.
    private static bool IsCovered(AssistantTopic topic, List<Message> answers) =>
        answers.Any(a => a.TopicKey == topic.Key && (topic.Kind != TopicKind.Choice || a.QuickReplyKey is not null));
}
