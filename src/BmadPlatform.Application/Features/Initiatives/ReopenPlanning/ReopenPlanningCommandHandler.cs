using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.ReopenPlanning;

public sealed class ReopenPlanningCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<ReopenPlanningCommand>
{
    public async Task Handle(ReopenPlanningCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await repository.GetAsync(request.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

        initiative.ReopenPlanning(timeProvider.GetUtcNow());
        await repository.UpdateAsync(initiative, cancellationToken);
    }
}
