using FluentValidation;

namespace RutaProducto.Application.Features.Initiatives.SaveInitiativeDetails;

public sealed class SaveInitiativeDetailsCommandValidator : AbstractValidator<SaveInitiativeDetailsCommand>
{
    public SaveInitiativeDetailsCommandValidator()
    {
        RuleFor(command => command.Name).ValidInitiativeName();
        RuleFor(command => command.Description).ValidInitiativeDescription();
    }
}
