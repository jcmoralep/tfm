using MediatR;

namespace RutaProducto.Application.Features.Initiatives.ReopenPlanning;

/// <summary>Moves a ReadyToBuild initiative back to Planning after a change to its work; idempotent.</summary>
public sealed record ReopenPlanningCommand(Guid Id) : IRequest;
