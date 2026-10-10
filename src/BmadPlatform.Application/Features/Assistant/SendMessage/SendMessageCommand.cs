using MediatR;

namespace BmadPlatform.Application.Features.Assistant.SendMessage;

/// <summary>
/// The user's answer to the current question: free text or the key of one of the offered quick replies, never both.
/// <paramref name="ExpectedVersion"/> is the conversation version the page was showing; a stale one is rejected.
/// </summary>
public sealed record SendMessageCommand(Guid InitiativeId, int ExpectedVersion, string? Text, string? QuickReplyKey)
    : IRequest<ConversationView>
{
    // The synthesized ToString would print the answer, which can contain initiative information.
    public override string ToString() =>
        $"{nameof(SendMessageCommand)} {{ InitiativeId = {InitiativeId}, ExpectedVersion = {ExpectedVersion}, " +
        $"Text = [redacted], QuickReplyKey = {QuickReplyKey} }}";
}
