using RutaProducto.Domain.Artifacts;
using MediatR;

namespace RutaProducto.Application.Features.Artifacts.ExportArtifact;

/// <summary>The document as a downloadable file; null when it does not exist or is not the current user's.</summary>
public sealed record ExportArtifactQuery(Guid InitiativeId, ArtifactType Type) : IRequest<ArtifactExport?>;
