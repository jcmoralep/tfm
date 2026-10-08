using BmadPlatform.Application.Abstractions.Authentication;
using MediatR;

namespace BmadPlatform.Application.Features.Authentication.SignOut;

public sealed record SignOutCommand : IRequest;

public sealed class SignOutCommandHandler(IAuthService authService) : IRequestHandler<SignOutCommand>
{
    public Task Handle(SignOutCommand request, CancellationToken cancellationToken) =>
        authService.SignOutAsync(cancellationToken);
}
