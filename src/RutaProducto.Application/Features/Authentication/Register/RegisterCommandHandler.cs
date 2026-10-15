using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Captcha;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.Register;

public sealed class RegisterCommandHandler(IAuthService authService, ICaptchaVerifier captchaVerifier)
    : IRequestHandler<RegisterCommand, RegistrationResult>
{
    public async Task<RegistrationResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // The captcha runs first: a bot learns nothing about accounts and no account is created without it.
        var captcha = await captchaVerifier.VerifyAsync(request.CaptchaToken, cancellationToken);

        if (captcha != CaptchaOutcome.Passed)
        {
            return captcha == CaptchaOutcome.Rejected
                ? RegistrationResult.CaptchaRejected
                : RegistrationResult.CaptchaUnavailable;
        }

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
