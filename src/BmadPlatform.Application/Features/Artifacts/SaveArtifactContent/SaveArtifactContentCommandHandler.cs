using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Initiatives.ReopenPlanning;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.SaveArtifactContent;

/// <summary>
/// Reopening Planning goes before the save: a failed save then leaves Planning with approved documents (a state the
/// next approval completes), never Ready to build with a Draft document. A save that changes nothing touches nothing.
/// </summary>
public sealed class SaveArtifactContentCommandHandler(
    ISender sender,
    IArtifactRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<SaveArtifactContentCommand, ArtifactView>
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

        if (!string.Equals(content, artifact.Content, StringComparison.Ordinal))
        {
            if (initiative.Status == InitiativeStatus.ReadyToBuild)
            {
                await sender.Send(new ReopenPlanningCommand(initiative.Id), cancellationToken);
            }

            artifact.Edit(content, timeProvider.GetUtcNow());
            await repository.SaveAsync(artifact, request.ExpectedVersion, cancellationToken);
        }

        return await ArtifactGuards.ReloadViewAsync(sender, repository, ownerId, initiative.Id, request.Type, cancellationToken);
    }
}
