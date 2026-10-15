using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Captcha;
using RutaProducto.Application.Abstractions.Email;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.Register;

public sealed class RegisterCommandHandler(
    IAuthService authService,
    ICaptchaVerifier captchaVerifier,
    IAccountTokenService tokens,
    IAccountLinks links,
    IEmailSender emailSender,
    AccountEmailSettings settings)
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

        return settings.RequireConfirmedEmail
            ? await ConfirmByEmailAsync(email, result, cancellationToken)
            : await SignInAsync(email, request.Password, result, cancellationToken);
    }

    // Switch off: exactly the original flow. EmailTaken is shown and nothing is emailed.
    private async Task<RegistrationResult> SignInAsync(
        string email, string password, RegistrationResult result, CancellationToken cancellationToken)
    {
        if (result.Status == RegistrationStatus.Succeeded)
        {
            // New people land inside the platform; if this sign-in fails they simply see the login page.
            await authService.SignInAsync(email, password, rememberMe: false, cancellationToken);
        }

        return result;
    }

    // Switch on: a new and a duplicate address get the same answer, and nobody is signed in before confirming.
    private async Task<RegistrationResult> ConfirmByEmailAsync(
        string email, RegistrationResult result, CancellationToken cancellationToken)
    {
        switch (result.Status)
        {
            case RegistrationStatus.Succeeded:
                var token = await tokens.CreateEmailConfirmationAsync(email, cancellationToken);

                if (token is not null)
                {
                    emailSender.Enqueue(AccountEmails.ConfirmEmail(token.Email,links.EmailConfirmation(token)));
                }

                return RegistrationResult.ConfirmationPending;

            case RegistrationStatus.EmailTaken:
                // Mail the address stored on the existing account, never the typed one.
                var stored = await tokens.FindStoredEmailAsync(email, cancellationToken);

                if (stored is not null)
                {
                    emailSender.Enqueue(AccountEmails.AccountExists(stored, links.Login(), links.ForgotPassword()));
                }

                return RegistrationResult.ConfirmationPending;

            default:
                return result;
        }
    }
}
