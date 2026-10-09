using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.UpdateInitiative;

/// <summary>Edit page. Deliberately has no status member: status is never edited by hand.</summary>
public sealed record UpdateInitiativeCommand(
    Guid Id,
    string Name,
    string? Description,
    DepthMode? DepthMode,
    InitiativeDepth? Depth) : IRequest
{
    // Records print every property by default; the name and description may hold initiative content.
    public override string ToString() => $"{nameof(UpdateInitiativeCommand)} {{ Id = {Id} }}";
}
