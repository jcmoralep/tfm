using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Features.Authentication.Register;
using FluentValidation;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.ResetPassword;

/// <summary>Sets a new password from the emailed link. Every problem with the link is the same result.</summary>
public sealed record ResetPasswordCommand(string UserId, string Code, string Password, string ConfirmPassword)
    : IRequest<PasswordResetResult>
{
    // The code is a credential and the passwords are secrets; never print them.
    public override string ToString() => nameof(ResetPasswordCommand);
}

/// <summary>
/// Same shape rules and Spanish messages as registration. The strength rules (<see cref="PasswordPolicy"/>) are enforced by
/// Identity after the token check, so a weak password leaves the link usable.
/// </summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(RegisterCommandValidator.MaxPasswordLength).WithMessage("La contraseña es demasiado larga.");

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.Password).WithMessage("Las contraseñas no coinciden.");
    }
}

public sealed class ResetPasswordCommandHandler(IAccountTokenService tokens) : IRequestHandler<ResetPasswordCommand, PasswordResetResult>
{
    public Task<PasswordResetResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken) =>
        tokens.ResetPasswordAsync(request.UserId, request.Code, request.Password, cancellationToken);
}
