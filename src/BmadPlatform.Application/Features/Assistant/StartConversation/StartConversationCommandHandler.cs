using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Assistant;
using MediatR;

namespace BmadPlatform.Application.Features.Assistant.StartConversation;

public sealed class StartConversationCommandHandler(
    ISender sender,
    IConversationRepository repository,
    ConversationAdvancer advancer,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<StartConversationCommand, ConversationView>
{
    public async Task<ConversationView> Handle(StartConversationCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ConversationGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        ConversationGuards.RequireOpen(initiative.Status);

        var conversation = await repository.GetAsync(initiative.Id, ownerId, cancellationToken)
            ?? await CreateAsync(initiative.Id, ownerId, cancellationToken);

        return await advancer.AdvanceAsync(conversation, initiative, cancellationToken);
    }

    private async Task<Conversation> CreateAsync(Guid initiativeId, string ownerId, CancellationToken cancellationToken)
    {
        var created = Conversation.Start(initiativeId, ownerId, timeProvider.GetUtcNow());

        try
        {
            await repository.AddAsync(created, cancellationToken);

            return created;
        }
        catch (ConflictException)
        {
            // Another tab (or the prerender pass) created it first: use that one.
            return await repository.GetAsync(initiativeId, ownerId, cancellationToken)
                ?? throw new ConflictException();
        }
    }
}
