using BmadPlatform.Domain.Artifacts;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.RegenerateArtifact;

/// <summary>
/// Replaces the whole document, manual edits included, with a new draft. <paramref name="Confirmed"/> is set by the
/// confirmation panel; without it the command is rejected and nothing changes.
/// </summary>
public sealed record RegenerateArtifactCommand(Guid InitiativeId, ArtifactType Type, int ExpectedVersion, bool Confirmed)
    : IRequest<ArtifactView>;
