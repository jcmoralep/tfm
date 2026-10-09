using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Assistant;

/// <summary>The single projection of a conversation, shared by the query and the commands.</summary>
public static class ConversationViewBuilder
{
    public static ConversationView Build(InitiativeDetails initiative, Conversation? conversation)
    {
        var journey = ConversationJourney.Compute(JourneyOf(initiative), conversation?.Messages ?? []);
        var visible = conversation?.VisibleMessages ?? [];
        var last = conversation?.LastVisible;
        var isOpen = IsOpen(initiative.Status);
        var inSync = IsInSync(last, journey);

        return new ConversationView(
            initiative.Id,
            initiative.Name,
            initiative.Status,
            initiative.DepthMode,
            initiative.Depth,
            conversation is not null,
            conversation?.Version ?? 0,
            [.. visible.Select(m => new MessageView(m.Sequence, m.Role, m.Content, m.AnswerKind))],
            isOpen && last is { Role: MessageRole.Assistant } ? last.QuickReplies : [],
            journey,
            CanSend: isOpen && inSync && journey.NextTopic is { Kind: not TopicKind.Closing },
            CanUndo: isOpen && conversation is { CanUndo: true },
            NeedsResume: isOpen && conversation is not null && !inSync,
            ReadOnly: initiative.Status == InitiativeStatus.ReadyToBuild);
    }

    internal static JourneyInput JourneyOf(InitiativeDetails initiative) =>
        new(initiative.Status, initiative.DepthMode, initiative.Depth);

    internal static bool IsOpen(InitiativeStatus status) => status is InitiativeStatus.Clarifying or InitiativeStatus.Planning;

    /// <summary>True when the last visible message is the assistant asking the topic the journey expects next.</summary>
    internal static bool IsInSync(Message? last, JourneySnapshot journey) =>
        last is { Role: MessageRole.Assistant } && journey.NextTopic is { } next && last.TopicKey == next.Key;
}
