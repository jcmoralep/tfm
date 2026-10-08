using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.UpdateInitiative;

public sealed class UpdateInitiativeCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<UpdateInitiativeCommand>
{
    public async Task Handle(UpdateInitiativeCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var initiative = await repository.GetAsync(request.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

        initiative.Rename(request.Name, request.Description, now);

        // Mode and depth are locked outside Draft and Clarifying, so only touch them when the user changed them.
        var requestedDepth = request.DepthMode == DepthMode.Automatic ? null : request.Depth;
        if (initiative.DepthMode != request.DepthMode || initiative.Depth != requestedDepth)
        {
            initiative.SetDepth(request.DepthMode, requestedDepth, now);
        }

        await repository.UpdateAsync(initiative, cancellationToken);
    }
}
