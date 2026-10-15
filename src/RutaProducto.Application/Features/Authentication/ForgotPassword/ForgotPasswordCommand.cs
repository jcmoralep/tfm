using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Captcha;
using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Application.Features.Authentication.SignIn;
using FluentValidation;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.ForgotPassword;

public enum ForgotPasswordResult
{
    /// <summary>Always returned once the captcha passed, whether or not the address has an account.</summary>
    Accepted,
    CaptchaRejected,
    CaptchaUnavailable,
}

/// <summary>Asks for a password reset link. The answer never says whether the address has an account.</summary>
public sealed record ForgotPasswordCommand(string Email, string? CaptchaToken = null) : IRequest<ForgotPasswordResult>
{
    // Records print every property by default; never expose the address or the captcha token.
    public override string ToString() => nameof(ForgotPasswordCommand);
}

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .MaximumLength(SignInCommandValidator.MaxEmailLength).WithMessage("El correo electrónico es demasiado largo.")
            .MustBeAsciiAddress()
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.");
    }
}

public sealed class ForgotPasswordCommandHandler(
    ICaptchaVerifier captchaVerifier,
    IAccountTokenService tokens,
    IAccountLinks links,
    IEmailSender emailSender,
    AccountEmailSettings settings) : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    public async Task<ForgotPasswordResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // The captcha runs before any lookup, so a bot learns nothing and cannot flood the mailboxes.
        var captcha = await captchaVerifier.VerifyAsync(request.CaptchaToken, cancellationToken);

        if (captcha != CaptchaOutcome.Passed)
        {
            return captcha == CaptchaOutcome.Rejected
                ? ForgotPasswordResult.CaptchaRejected
                : ForgotPasswordResult.CaptchaUnavailable;
        }

        if (!settings.EmailEnabled)
        {
            return ForgotPasswordResult.Accepted;
        }

        var email = request.Email.Trim();

        // Null only for an unknown address: nothing is sent and nothing differs for the caller.
        // The mail goes to the STORED address (token.Email), never to the typed one.
        var token = await tokens.CreatePasswordResetAsync(email, cancellationToken);

        if (token is not null)
        {
            emailSender.Enqueue(AccountEmails.ResetPassword(token.Email,links.PasswordReset(token)));
        }

        return ForgotPasswordResult.Accepted;
    }
}
