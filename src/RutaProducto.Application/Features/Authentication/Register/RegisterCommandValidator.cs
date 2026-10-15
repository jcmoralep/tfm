using RutaProducto.Application.Features.Authentication.SignIn;
using FluentValidation;

namespace RutaProducto.Application.Features.Authentication.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public const int MaxPasswordLength = 128;

    public RegisterCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .MaximumLength(SignInCommandValidator.MaxEmailLength).WithMessage("El correo electrónico es demasiado largo.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.");

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(MaxPasswordLength).WithMessage("La contraseña es demasiado larga.");

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.Password).WithMessage("Las contraseñas no coinciden.");
    }
}
