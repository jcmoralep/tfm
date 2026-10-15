using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Domain.Initiatives;
using MediatR;

namespace RutaProducto.Application.Features.Initiatives.SetInitiativeDepth;

public sealed class SetInitiativeDepthCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<SetInitiativeDepthCommand>
{
    public async Task Handle(SetInitiativeDepthCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await repository.GetAsync(request.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

        initiative.SetDepth(initiative.DepthMode, request.Depth, timeProvider.GetUtcNow());
        await repository.UpdateAsync(initiative, cancellationToken);
    }
}
