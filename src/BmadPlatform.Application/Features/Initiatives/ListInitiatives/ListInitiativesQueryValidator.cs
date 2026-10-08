using FluentValidation;

namespace BmadPlatform.Application.Features.Initiatives.ListInitiatives;

public sealed class ListInitiativesQueryValidator : AbstractValidator<ListInitiativesQuery>
{
    public ListInitiativesQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(ListInitiativesQuery.SearchMaxLength)
            .WithMessage($"La búsqueda no puede superar los {ListInitiativesQuery.SearchMaxLength} caracteres.");

        RuleFor(query => query.Status).IsInEnum().WithMessage("El estado no es válido.");
        RuleFor(query => query.Depth).IsInEnum().WithMessage("El nivel de profundidad no es válido.");
    }
}
