using RutaProducto.Application.Abstractions.Authentication;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.Register;

public sealed class RegisterCommandHandler(IAuthService authService)
    : IRequestHandler<RegisterCommand, RegistrationResult>
{
    public async Task<RegistrationResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var result = await authService.RegisterAsync(email, request.Password, cancellationToken);

        if (result.Status == RegistrationStatus.Succeeded)
        {
            // New people land inside the platform; if this sign-in fails they simply see the login page.
            await authService.SignInAsync(email, request.Password, rememberMe: false, cancellationToken);
        }

        return result;
    }
}
