using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>A stored document without its content, enough to decide whether a required set is complete.</summary>
public sealed record ArtifactSummary(ArtifactType Type, ArtifactStatus Status, int Version);
