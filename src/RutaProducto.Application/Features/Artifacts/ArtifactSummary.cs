using RutaProducto.Domain.Artifacts;

namespace RutaProducto.Application.Features.Artifacts;

/// <summary>
/// A stored document without its content, enough to decide whether a required set is complete and whether a
/// dependent document is older than the one it depends on.
/// </summary>
public sealed record ArtifactSummary(ArtifactType Type, ArtifactStatus Status, int Version, DateTimeOffset UpdatedAt);
