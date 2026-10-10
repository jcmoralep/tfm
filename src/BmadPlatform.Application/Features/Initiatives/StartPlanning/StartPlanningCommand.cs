using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.StartPlanning;

/// <summary>Moves a Clarifying initiative to Planning. Sent only by the assistant after the user's confirmation; idempotent.</summary>
public sealed record StartPlanningCommand(Guid Id) : IRequest;
