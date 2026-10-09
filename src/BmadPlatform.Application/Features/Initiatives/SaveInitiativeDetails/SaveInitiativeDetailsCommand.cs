using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.SaveInitiativeDetails;

/// <summary>Wizard step 1. Creates the draft when <paramref name="Id"/> is null; returns the initiative id.</summary>
public sealed record SaveInitiativeDetailsCommand(Guid? Id, string Name, string? Description, bool Advance)
    : IRequest<Guid>
{
    // Records print every property by default; the name and description may hold initiative content.
    public override string ToString() => $"{nameof(SaveInitiativeDetailsCommand)} {{ Id = {Id} }}";
}
