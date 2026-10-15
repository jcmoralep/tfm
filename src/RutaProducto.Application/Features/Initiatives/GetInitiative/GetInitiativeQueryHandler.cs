using RutaProducto.Application.Abstractions.Authentication;
using MediatR;

namespace RutaProducto.Application.Features.Initiatives.GetInitiative;

public sealed class GetInitiativeQueryHandler(IInitiativeRepository repository, ICurrentUser currentUser)
    : IRequestHandler<GetInitiativeQuery, InitiativeDetails?>
{
    public async Task<InitiativeDetails?> Handle(GetInitiativeQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        return await repository.GetDetailsAsync(request.Id, ownerId, cancellationToken);
    }
}
