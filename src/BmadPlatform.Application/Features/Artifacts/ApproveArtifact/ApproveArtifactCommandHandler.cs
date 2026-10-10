using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Initiatives.MarkReadyToBuild;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.ApproveArtifact;

/// <summary>
/// The approval is stored first and the initiative is marked ready afterwards. A failure in between leaves Planning
/// with every document approved, which a repeated approval completes; the opposite order could leave Ready to build
/// with a Draft document. The completion check runs on every approval, including a repeated one.
/// </summary>
public sealed class ApproveArtifactCommandHandler(
    ISender sender,
    IArtifactRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<ApproveArtifactCommand, ArtifactView>
{
    public async Task<ArtifactView> Handle(ApproveArtifactCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ArtifactGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        ArtifactGuards.RequireManageable(initiative, request.Type);

        var artifact = ArtifactGuards.RequireGenerated(
            await repository.GetAsync(initiative.Id, request.Type, ownerId, cancellationToken));
        ArtifactGuards.RequireVersion(artifact, request.ExpectedVersion);

        if (artifact.Status != ArtifactStatus.Approved)
        {
            artifact.Approve(timeProvider.GetUtcNow());
            await repository.SaveAsync(artifact, request.ExpectedVersion, cancellationToken);
        }

        var summaries = await repository.ListAsync(initiative.Id, ownerId, cancellationToken);

        if (initiative.Status == InitiativeStatus.Planning && ArtifactCatalog.IsSetComplete(initiative.Depth, summaries))
        {
            await sender.Send(new MarkReadyToBuildCommand(initiative.Id), cancellationToken);
        }

        return await ArtifactGuards.ReloadViewAsync(sender, repository, ownerId, initiative.Id, request.Type, cancellationToken);
    }
}
