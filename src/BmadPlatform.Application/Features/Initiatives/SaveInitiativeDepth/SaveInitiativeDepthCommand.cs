using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.SaveInitiativeDepth;

/// <summary>Wizard step 2. Stores the depth choice made so far and the step to reopen.</summary>
public sealed record SaveInitiativeDepthCommand(Guid Id, DepthMode? DepthMode, InitiativeDepth? Depth, bool Advance)
    : IRequest;
