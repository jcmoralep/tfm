using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.GetArtifactsOverview;

/// <summary>
/// Read only. Returns <c>null</c> when the initiative does not exist for the caller (missing, deleted or foreign).
/// Never generates anything.
/// </summary>
public sealed record GetArtifactsOverviewQuery(Guid InitiativeId) : IRequest<ArtifactsOverview?>;
