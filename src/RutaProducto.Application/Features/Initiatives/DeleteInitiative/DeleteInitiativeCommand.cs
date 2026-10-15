using MediatR;

namespace RutaProducto.Application.Features.Initiatives.DeleteInitiative;

/// <summary>Soft delete.</summary>
public sealed record DeleteInitiativeCommand(Guid Id) : IRequest;
