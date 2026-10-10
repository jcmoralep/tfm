using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.MarkReadyToBuild;

public sealed class MarkReadyToBuildCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<MarkReadyToBuildCommand>
{
    public async Task Handle(MarkReadyToBuildCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await repository.GetAsync(request.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

        initiative.MarkReadyToBuild(timeProvider.GetUtcNow());
        await repository.UpdateAsync(initiative, cancellationToken);
    }
}
