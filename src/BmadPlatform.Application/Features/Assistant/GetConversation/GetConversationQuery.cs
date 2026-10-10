using MediatR;

namespace BmadPlatform.Application.Features.Assistant.GetConversation;

/// <summary>
/// Read only. Returns <c>null</c> when the initiative does not exist for the caller (missing, deleted or foreign);
/// <c>Started = false</c> in the view when there is no conversation yet.
/// </summary>
public sealed record GetConversationQuery(Guid InitiativeId) : IRequest<ConversationView?>;
