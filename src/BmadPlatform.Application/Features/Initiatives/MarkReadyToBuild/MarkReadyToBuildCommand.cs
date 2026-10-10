using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.MarkReadyToBuild;

/// <summary>Moves a Planning initiative to ReadyToBuild. The caller checks completeness beforehand; idempotent.</summary>
public sealed record MarkReadyToBuildCommand(Guid Id) : IRequest;
