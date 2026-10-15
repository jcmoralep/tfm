using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Domain.Initiatives;
using MediatR;

namespace RutaProducto.Application.Features.Initiatives.UpdateInitiative;

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
        if (initiative.DepthMode != request.DepthMode || initiative.Depth != request.Depth)
        {
            initiative.SetDepth(request.DepthMode, request.Depth, now);
        }

        await repository.UpdateAsync(initiative, cancellationToken);
    }
}
