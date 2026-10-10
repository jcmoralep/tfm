using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Assistant;

public sealed record MessageView(int Sequence, MessageRole Role, string Content, AnswerKind? AnswerKind);

/// <summary>
/// Everything the chat page needs, projected from the conversation and the initiative.
/// </summary>
/// <param name="Started">False before the first visit: there is no conversation yet.</param>
/// <param name="Version">The value to send back as <c>ExpectedVersion</c>; 0 when not started.</param>
/// <param name="Messages">Visible messages in order.</param>
/// <param name="QuickReplies">The buttons of the last assistant message, label and key.</param>
/// <param name="CanSend">An answer is expected and accepted now.</param>
/// <param name="CanUndo">The last answer can be hidden.</param>
/// <param name="NeedsResume">Starting the conversation would add something (opening question, pending reply, re-sync).</param>
/// <param name="ReadOnly">The initiative is ready to build: history only.</param>
public sealed record ConversationView(
    Guid InitiativeId,
    string InitiativeName,
    InitiativeStatus Status,
    DepthMode? DepthMode,
    InitiativeDepth? Depth,
    bool Started,
    int Version,
    IReadOnlyList<MessageView> Messages,
    IReadOnlyList<QuickReply> QuickReplies,
    JourneySnapshot Journey,
    bool CanSend,
    bool CanUndo,
    bool NeedsResume,
    bool ReadOnly);
