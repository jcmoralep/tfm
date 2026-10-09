using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using MediatR;

namespace BmadPlatform.Application.Features.Assistant.GetConversation;

public sealed class GetConversationQueryHandler(
    ISender sender,
    IConversationRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetConversationQuery, ConversationView?>
{
    public async Task<ConversationView?> Handle(GetConversationQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await sender.Send(new GetInitiativeQuery(request.InitiativeId), cancellationToken);

        if (initiative is null)
        {
            return null;
        }

        var conversation = await repository.GetAsync(request.InitiativeId, ownerId, cancellationToken);

        return ConversationViewBuilder.Build(initiative, conversation);
    }
}
