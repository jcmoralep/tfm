using MediatR;

namespace RutaProducto.Application.Features.Context.GetContext;

/// <summary>Returns <c>null</c> when the initiative does not exist for the caller (missing, deleted or foreign).</summary>
public sealed record GetContextQuery(Guid InitiativeId) : IRequest<ContextView?>;
