using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Assistant.GetConversationAnswers;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.GetArtifactsOverview;

public sealed class GetArtifactsOverviewQueryHandler(
    ISender sender,
    IArtifactRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetArtifactsOverviewQuery, ArtifactsOverview?>
{
    public async Task<ArtifactsOverview?> Handle(GetArtifactsOverviewQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await sender.Send(new GetInitiativeQuery(request.InitiativeId), cancellationToken);

        if (initiative is null)
        {
            return null;
        }

        var answers = await sender.Send(new GetConversationAnswersQuery(request.InitiativeId), cancellationToken);
        var summaries = await repository.ListAsync(request.InitiativeId, ownerId, cancellationToken);

        return ArtifactViewBuilder.BuildOverview(initiative, answers, summaries);
    }
}
