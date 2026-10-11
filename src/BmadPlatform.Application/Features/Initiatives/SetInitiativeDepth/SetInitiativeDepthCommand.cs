using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;

/// <summary>Sets the level and keeps the current mode, used when the person accepts or chooses a level in the conversation.</summary>
public sealed record SetInitiativeDepthCommand(Guid Id, InitiativeDepth Depth) : IRequest;
