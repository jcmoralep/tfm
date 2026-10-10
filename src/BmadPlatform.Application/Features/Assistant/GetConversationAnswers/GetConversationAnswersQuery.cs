using MediatR;

namespace BmadPlatform.Application.Features.Assistant.GetConversationAnswers;

/// <summary>
/// Read only, for other modules. Returns <c>null</c> when the initiative does not exist for the caller (missing,
/// deleted or foreign); empty answers and both flags false when there is no conversation yet.
/// </summary>
public sealed record GetConversationAnswersQuery(Guid InitiativeId) : IRequest<ConversationAnswers?>;
