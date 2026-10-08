using FluentValidation;

namespace BmadPlatform.Application.Features.Authentication.SignIn;

public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public const int MaxEmailLength = 256;

    public SignInCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .MaximumLength(MaxEmailLength).WithMessage("El correo electrónico es demasiado largo.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
