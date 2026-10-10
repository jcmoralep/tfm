using BmadPlatform.Domain.Artifacts;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.ApproveArtifact;

/// <summary>
/// Approves a draft at the version the page was showing. Idempotent at the same version, so sending it again also
/// completes an initiative whose ready transition failed after the approval was stored.
/// </summary>
public sealed record ApproveArtifactCommand(Guid InitiativeId, ArtifactType Type, int ExpectedVersion) : IRequest<ArtifactView>;
