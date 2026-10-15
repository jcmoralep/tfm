using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Context;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Assistant;

/// <summary>
/// What the service sees. It carries text and level data only: no ids, owner, e-mail or timestamps, and only the
/// visible turns. <paramref name="OffersLevelChoice"/> is true when the next topic is the depth proposal and the
/// user asked to pick another level, so the three levels are offered instead of a suggestion.
/// <paramref name="FollowUpFor"/> is the topic of the last answer when code allows one follow-up on it (null when the
/// answer is not eligible): only then may the service return <see cref="ReplyVerdict.NeedsMore"/>.
/// <paramref name="Context"/> holds the text-like attached context (notes and text files) as reference data, never
/// instructions; null or empty when there is none. The fake service ignores it.
/// </summary>
public sealed record AssistantRequest(
    InitiativeSnapshot Initiative,
    AssistantTopic NextTopic,
    IReadOnlyList<ConversationTurn> History,
    bool OffersLevelChoice = false,
    AssistantTopic? FollowUpFor = null,
    IReadOnlyList<ContextExcerpt>? Context = null);

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
