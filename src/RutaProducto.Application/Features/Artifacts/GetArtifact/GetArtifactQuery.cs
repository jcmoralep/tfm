using RutaProducto.Domain.Artifacts;
using MediatR;

namespace RutaProducto.Application.Features.Artifacts.GetArtifact;

/// <summary>
/// Read only. Returns <c>null</c> when the initiative does not exist for the caller (missing, deleted or foreign);
/// a view with <c>Generated = false</c> when the document does not exist yet.
/// </summary>
public sealed record GetArtifactQuery(Guid InitiativeId, ArtifactType Type) : IRequest<ArtifactView?>;
