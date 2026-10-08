using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.CompleteInitiative;

public sealed class CompleteInitiativeCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<CompleteInitiativeCommand>
{
    public async Task Handle(CompleteInitiativeCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await repository.GetAsync(request.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

        initiative.Complete(timeProvider.GetUtcNow());
        await repository.UpdateAsync(initiative, cancellationToken);
    }
}
