using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.GenerateArtifact;

/// <summary>
/// Drafts and stores version 1. The unique (initiative, type) row is what stops two tabs generating at once: the
/// slower one gets the conflict message from the repository. Nothing is stored when the drafter fails.
/// </summary>
public sealed class GenerateArtifactCommandHandler(
    ISender sender,
    IArtifactRepository repository,
    IArtifactDrafter drafter,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<GenerateArtifactCommand, ArtifactView>
{
    public async Task<ArtifactView> Handle(GenerateArtifactCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ArtifactGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        var answers = await ArtifactGuards.GetAnswersAsync(sender, request.InitiativeId, cancellationToken);

        var summaries = await repository.ListAsync(initiative.Id, ownerId, cancellationToken);
        ArtifactGuards.RequireAvailable(ArtifactCatalog.Availability(request.Type, initiative.Status, initiative.Depth, answers, summaries));

        if (await repository.GetAsync(initiative.Id, request.Type, ownerId, cancellationToken) is not null)
        {
            throw new DomainException(ArtifactTexts.AlreadyExists);
        }

        var draftRequest = await ArtifactGuards.DraftRequestAsync(sender, repository, initiative, ownerId, request.Type, answers, cancellationToken);
        var markdown = await drafter.DraftAsync(draftRequest, cancellationToken);

        var artifact = Artifact.Create(initiative.Id, ownerId, request.Type, markdown, timeProvider.GetUtcNow());
        await repository.AddAsync(artifact, cancellationToken);

        return await ArtifactGuards.ReloadViewAsync(sender, repository, ownerId, initiative.Id, request.Type, cancellationToken);
    }
}
