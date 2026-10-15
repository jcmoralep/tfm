using MediatR;

namespace BmadPlatform.Application.Features.Context.GetContextForAssistant;

/// <summary>
/// The text-like context of an initiative, ready for the model: oldest first, within the caps. Empty (never null) when
/// there is nothing to send or the initiative does not exist for the caller.
/// </summary>
public sealed record GetContextForAssistantQuery(Guid InitiativeId) : IRequest<IReadOnlyList<ContextExcerpt>>;
