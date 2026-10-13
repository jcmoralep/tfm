using BmadPlatform.Domain.Artifacts;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.ExportArtifact;

/// <summary>The document as a downloadable file; null when it does not exist or is not the current user's.</summary>
public sealed record ExportArtifactQuery(Guid InitiativeId, ArtifactType Type) : IRequest<ArtifactExport?>;
