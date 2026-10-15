using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Features.Initiatives.ReopenPlanning;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Initiatives;
using MediatR;

namespace RutaProducto.Application.Features.Artifacts.RegenerateArtifact;

/// <summary>
/// The confirmation is checked after the initiative is resolved, so a foreign id never learns more than "not found".
/// The draft is made and validated before Planning is reopened, and the reopen goes before the save, like an edit.
/// </summary>
public sealed class RegenerateArtifactCommandHandler(
    ISender sender,
    IArtifactRepository repository,
    IArtifactDrafter drafter,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<RegenerateArtifactCommand, ArtifactView>
{
    public async Task<ArtifactView> Handle(RegenerateArtifactCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ArtifactGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        ArtifactGuards.RequireManageable(initiative, request.Type);

        if (!request.Confirmed)
        {
            throw new DomainException(ArtifactTexts.ConfirmReplace);
        }

        var artifact = ArtifactGuards.RequireGenerated(
            await repository.GetAsync(initiative.Id, request.Type, ownerId, cancellationToken));
        ArtifactGuards.RequireVersion(artifact, request.ExpectedVersion);

        var answers = await ArtifactGuards.GetAnswersAsync(sender, request.InitiativeId, cancellationToken);
        var summaries = await repository.ListAsync(initiative.Id, ownerId, cancellationToken);
        ArtifactGuards.RequireAvailable(ArtifactCatalog.Availability(request.Type, initiative.Status, initiative.Depth, answers, summaries));

        var draftRequest = await ArtifactGuards.DraftRequestAsync(sender, repository, initiative, ownerId, request.Type, answers, cancellationToken);
        var markdown = await drafter.DraftAsync(draftRequest, cancellationToken);
        var content = Artifact.NormalizeContent(markdown);

        if (initiative.Status == InitiativeStatus.ReadyToBuild)
        {
            await sender.Send(new ReopenPlanningCommand(initiative.Id), cancellationToken);
        }

        artifact.Replace(content, timeProvider.GetUtcNow());
        await repository.SaveAsync(artifact, request.ExpectedVersion, cancellationToken);

        return await ArtifactGuards.ReloadViewAsync(sender, repository, ownerId, initiative.Id, request.Type, cancellationToken);
    }
}
