using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Application.Features.Authentication.SignIn;
using FluentValidation;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.ResendConfirmation;

/// <summary>Asks for a new confirmation link. The answer is the same for unknown, confirmed and pending addresses.</summary>
public sealed record ResendConfirmationCommand(string Email) : IRequest<Unit>
{
    public override string ToString() => nameof(ResendConfirmationCommand);
}

public sealed class ResendConfirmationCommandValidator : AbstractValidator<ResendConfirmationCommand>
{
    public ResendConfirmationCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .MaximumLength(SignInCommandValidator.MaxEmailLength).WithMessage("El correo electrónico es demasiado largo.")
            .MustBeAsciiAddress()
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.");
    }
}

public sealed class ResendConfirmationCommandHandler(
    IAccountTokenService tokens,
    IAccountLinks links,
    IEmailSender emailSender,
    AccountEmailSettings settings) : IRequestHandler<ResendConfirmationCommand, Unit>
{
    public async Task<Unit> Handle(ResendConfirmationCommand request, CancellationToken cancellationToken)
    {
        if (!settings.EmailEnabled)
        {
            return Unit.Value;
        }

        var email = request.Email.Trim();

        // Null for an unknown or an already confirmed address: nothing is sent and nothing differs for the caller.
        var token = await tokens.CreateEmailConfirmationAsync(email, cancellationToken);

        if (token is not null)
        {
            emailSender.Enqueue(AccountEmails.ConfirmEmail(token.Email,links.EmailConfirmation(token)));
        }

        return Unit.Value;
    }
}
