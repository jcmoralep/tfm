using MediatR;

namespace BmadPlatform.Application.Features.Assistant.StartConversation;

/// <summary>
/// Opens the conversation, or resumes it: creates it on the first call, then completes whatever is missing
/// (opening question, pending reply, stored confirmation). Idempotent.
/// </summary>
public sealed record StartConversationCommand(Guid InitiativeId) : IRequest<ConversationView>;
