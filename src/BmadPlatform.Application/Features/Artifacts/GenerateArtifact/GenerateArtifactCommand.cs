using BmadPlatform.Domain.Artifacts;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.GenerateArtifact;

/// <summary>Drafts a document that was not generated yet from the answers collected so far.</summary>
public sealed record GenerateArtifactCommand(Guid InitiativeId, ArtifactType Type) : IRequest<ArtifactView>;
