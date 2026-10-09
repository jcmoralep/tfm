using BmadPlatform.Application.Abstractions.Authentication;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.ListInitiatives;

public sealed class ListInitiativesQueryHandler(IInitiativeRepository repository, ICurrentUser currentUser)
    : IRequestHandler<ListInitiativesQuery, IReadOnlyList<InitiativeSummary>>
{
    public async Task<IReadOnlyList<InitiativeSummary>> Handle(ListInitiativesQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);
        var filter = new InitiativeListFilter(request.Search, request.Status, request.Depth);

        return await repository.ListAsync(ownerId, filter, cancellationToken);
    }
}
