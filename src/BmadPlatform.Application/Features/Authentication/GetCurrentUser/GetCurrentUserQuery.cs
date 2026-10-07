using BmadPlatform.Application.Abstractions.Authentication;
using MediatR;

namespace BmadPlatform.Application.Features.Authentication.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<AuthenticatedUser?>;

public sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser)
    : IRequestHandler<GetCurrentUserQuery, AuthenticatedUser?>
{
    public Task<AuthenticatedUser?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken) =>
        currentUser.GetAsync(cancellationToken);
}
