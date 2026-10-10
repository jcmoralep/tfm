using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;

/// <summary>Sets Manual mode and a level in one step, used when the user accepts the level the assistant suggested.</summary>
public sealed record SetInitiativeDepthCommand(Guid Id, InitiativeDepth Depth) : IRequest;
