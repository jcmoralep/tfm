using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Domain.Artifacts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Features.Artifacts.SaveArtifactContent;

/// <summary>
/// Reopening Planning goes before the save: a failed save then leaves Planning with approved documents (a state the
/// next approval completes), never Ready to build with a Draft document. A save that changes nothing touches nothing.
/// </summary>
public sealed class SaveArtifactContentCommandHandler(
    ISender sender,
    IArtifactRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<SaveArtifactContentCommandHandler> logger) : IRequestHandler<SaveArtifactContentCommand, ArtifactView>
{
    public async Task<ArtifactView> Handle(SaveArtifactContentCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ArtifactGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        ArtifactGuards.RequireManageable(initiative, request.Type);

        var artifact = ArtifactGuards.RequireGenerated(
            await repository.GetAsync(initiative.Id, request.Type, ownerId, cancellationToken));
        ArtifactGuards.RequireVersion(artifact, request.ExpectedVersion);

        // Validated before anything is written, so a rejected text cannot leave the initiative reopened.
        var content = Artifact.NormalizeContent(request.Content);

        await ArtifactGuards.SaveEditAsync(sender, repository, timeProvider, logger, initiative, artifact, content, request.ExpectedVersion, cancellationToken);

        return await ArtifactGuards.ReloadViewAsync(sender, repository, ownerId, initiative.Id, request.Type, cancellationToken);
    }
}
