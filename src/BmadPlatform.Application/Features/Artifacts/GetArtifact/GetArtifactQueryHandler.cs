using BmadPlatform.Application.Abstractions.Authentication;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.GetArtifact;

public sealed class GetArtifactQueryHandler(
    ISender sender,
    IArtifactRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetArtifactQuery, ArtifactView?>
{
    public async Task<ArtifactView?> Handle(GetArtifactQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        return await ArtifactGuards.LoadViewAsync(sender, repository, ownerId, request.InitiativeId, request.Type, cancellationToken);
    }
}
