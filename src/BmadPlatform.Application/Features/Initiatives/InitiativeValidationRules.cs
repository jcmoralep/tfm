using BmadPlatform.Domain.Initiatives;
using FluentValidation;

namespace BmadPlatform.Application.Features.Initiatives;

/// <summary>Validation rules shared by the commands that carry a name, a description or a depth choice.</summary>
internal static class InitiativeValidationRules
{
    public static IRuleBuilderOptions<T, string> ValidInitiativeName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            // Length is measured after trimming, as the domain stores the trimmed name.
            .Must(name => name.Trim().Length <= Initiative.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {Initiative.NameMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> ValidInitiativeDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(Initiative.DescriptionMaxLength)
            .WithMessage($"La descripción no puede superar los {Initiative.DescriptionMaxLength} caracteres.");

    public static void ValidInitiativeDepthChoice<T>(
        this AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, DepthMode?>> mode,
        System.Linq.Expressions.Expression<Func<T, InitiativeDepth?>> depth)
    {
        var readMode = mode.Compile();

        validator.RuleFor(mode)
            .IsInEnum().WithMessage("El modo de profundidad no es válido.");

        validator.RuleFor(depth)
            .IsInEnum().WithMessage("El nivel de profundidad no es válido.");

        validator.RuleFor(depth)
            .Must((request, value) => !(readMode(request) == DepthMode.Automatic && value is not null))
            .WithMessage("En modo automático no se puede elegir un nivel de profundidad.");
    }
}
