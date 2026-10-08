using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.DeleteInitiative;

public sealed class DeleteInitiativeCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<DeleteInitiativeCommand>
{
    public async Task Handle(DeleteInitiativeCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        // A deleted initiative is not returned by the repository, so deleting twice is "not found".
        var initiative = await repository.GetAsync(request.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

        initiative.Delete(timeProvider.GetUtcNow());
        await repository.UpdateAsync(initiative, cancellationToken);
    }
}
