using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.SaveInitiativeDepth;

public sealed class SaveInitiativeDepthCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<SaveInitiativeDepthCommand>
{
    public async Task Handle(SaveInitiativeDepthCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var initiative = await repository.GetAsync(request.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

        initiative.SetDepth(request.DepthMode, request.Depth, now);
        initiative.MoveToStep(request.Advance ? CreationStep.Review : CreationStep.Depth, now);
        await repository.UpdateAsync(initiative, cancellationToken);
    }
}
