using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Initiatives.MarkReadyToBuild;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.ApproveArtifact;

/// <summary>
/// The approval is stored first and the initiative is marked ready afterwards. A failure in between leaves Planning
/// with every document approved, which a repeated approval completes; the opposite order could leave Ready to build
/// with a Draft document. The completion check runs on every approval, including a repeated one. Approving needs the
/// coverage of the document to still hold (an undone answer takes it back), and the initiative is completed only
/// while every document of the level is covered.
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

        var answers = await ArtifactGuards.GetAnswersAsync(sender, request.InitiativeId, cancellationToken);
        var current = await repository.ListAsync(initiative.Id, ownerId, cancellationToken);
        ArtifactGuards.RequireAvailable(ArtifactCatalog.Availability(request.Type, initiative.Status, initiative.Depth, answers, current));

        if (artifact.Status != ArtifactStatus.Approved)
        {
            // Only the Draft-to-Approved step is gated: a repeated approval and the completion retry must keep working.
            if (OpenQuestions.Unassigned(artifact.Content) is { Count: > 0 } unassigned)
            {
                throw new DomainException(ArtifactTexts.UnassignedQuestions(unassigned));
            }

            artifact.Approve(timeProvider.GetUtcNow());
            await repository.SaveAsync(artifact, request.ExpectedVersion, cancellationToken);
        }

        var summaries = await repository.ListAsync(initiative.Id, ownerId, cancellationToken);

        if (initiative.Status == InitiativeStatus.Planning
            && ArtifactCatalog.IsSetAvailable(initiative.Status, initiative.Depth, answers)
            && ArtifactCatalog.IsSetComplete(initiative.Depth, summaries))
        {
            await sender.Send(new MarkReadyToBuildCommand(initiative.Id), cancellationToken);
        }

        return await ArtifactGuards.ReloadViewAsync(sender, repository, ownerId, initiative.Id, request.Type, cancellationToken);
    }
}
