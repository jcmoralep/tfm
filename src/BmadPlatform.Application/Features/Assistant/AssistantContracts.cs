using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Assistant;

/// <summary>
/// What the service sees. It carries text and level data only: no ids, owner, e-mail or timestamps, and only the
/// visible turns. <paramref name="OffersLevelChoice"/> is true when the next topic is the depth proposal and the
/// user asked to pick another level, so the three levels are offered instead of a suggestion.
/// </summary>
public sealed record AssistantRequest(
    InitiativeSnapshot Initiative,
    AssistantTopic NextTopic,
    IReadOnlyList<ConversationTurn> History,
    bool OffersLevelChoice = false);

public sealed record InitiativeSnapshot(string Name, string? Description, DepthMode? Mode, InitiativeDepth? Depth);

public sealed record ConversationTurn(MessageRole Role, string Text, string TopicKey, string? QuickReplyKey, AnswerKind? Kind);

/// <summary>Whether the last answer was enough (Proceed) or deserves one follow-up (NeedsMore).</summary>
public enum ReplyVerdict
{
    Proceed,
    NeedsMore,
}

/// <param name="Text">The assistant's message.</param>
/// <param name="SuggestedDepth">The level proposed; set only when the next topic is the depth proposal.</param>
/// <param name="Verdict">Defaults to Proceed: the fake and every turn without a judgement never push back.</param>
public sealed record AssistantReply(string Text, InitiativeDepth? SuggestedDepth, ReplyVerdict Verdict = ReplyVerdict.Proceed);
