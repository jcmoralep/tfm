using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Domain.Artifacts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Features.Artifacts.AssignQuestionOwners;

/// <summary>
/// Every rule that can reject (unknown id, content cap) runs before anything is written, so a refused assignment
/// never leaves the initiative reopened; the write itself is the shared edit path of saving content.
/// </summary>
public sealed class AssignQuestionOwnersCommandHandler(
    ISender sender,
    IArtifactRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<AssignQuestionOwnersCommandHandler> logger) : IRequestHandler<AssignQuestionOwnersCommand, ArtifactView>
{
    public async Task<ArtifactView> Handle(AssignQuestionOwnersCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ArtifactGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        ArtifactGuards.RequireManageable(initiative, request.Type);

        var artifact = ArtifactGuards.RequireGenerated(
            await repository.GetAsync(initiative.Id, request.Type, ownerId, cancellationToken));
        ArtifactGuards.RequireVersion(artifact, request.ExpectedVersion);

        var content = Artifact.NormalizeContent(QuestionOwners.Apply(artifact.Content, request.Assignments));

        await ArtifactGuards.SaveEditAsync(sender, repository, timeProvider, logger, initiative, artifact, content, request.ExpectedVersion, cancellationToken);

        return await ArtifactGuards.ReloadViewAsync(sender, repository, ownerId, initiative.Id, request.Type, cancellationToken);
    }
}
