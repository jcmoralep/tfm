using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.GetInitiative;

/// <summary>Returns <c>null</c> when the initiative does not exist for the caller (missing, deleted or foreign).</summary>
public sealed record GetInitiativeQuery(Guid Id) : IRequest<InitiativeDetails?>;
