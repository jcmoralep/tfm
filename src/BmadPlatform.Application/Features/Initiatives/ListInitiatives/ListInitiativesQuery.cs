using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.ListInitiatives;

/// <summary>Lists the caller's initiatives, most recently modified first. Filters combine with AND.</summary>
public sealed record ListInitiativesQuery(string? Search, InitiativeStatus? Status, InitiativeDepth? Depth)
    : IRequest<IReadOnlyList<InitiativeSummary>>
{
    public const int SearchMaxLength = 120;

    // The search term is user input; keep it out of logs.
    public override string ToString() => nameof(ListInitiativesQuery);
}
