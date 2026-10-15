using RutaProducto.Application.Abstractions.Authentication;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.SignIn;

public sealed class SignInCommandHandler(IAuthService authService)
    : IRequestHandler<SignInCommand, AuthenticationResult>
{
    public Task<AuthenticationResult> Handle(SignInCommand request, CancellationToken cancellationToken) =>
        authService.SignInAsync(request.Email.Trim(), request.Password, request.RememberMe, cancellationToken);
}
