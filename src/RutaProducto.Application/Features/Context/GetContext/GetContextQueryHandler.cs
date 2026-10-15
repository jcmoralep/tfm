using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Features.Initiatives.GetInitiative;
using MediatR;

namespace RutaProducto.Application.Features.Context.GetContext;

public sealed class GetContextQueryHandler(
    ISender sender,
    IAttachmentRepository repository,
    ICurrentUser currentUser,
    ContextLimits limits) : IRequestHandler<GetContextQuery, ContextView?>
{
    public async Task<ContextView?> Handle(GetContextQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        if (await sender.Send(new GetInitiativeQuery(request.InitiativeId), cancellationToken) is null)
        {
            return null;
        }

        return ContextView.From(await repository.ListAsync(request.InitiativeId, ownerId, cancellationToken), limits);
    }
}
