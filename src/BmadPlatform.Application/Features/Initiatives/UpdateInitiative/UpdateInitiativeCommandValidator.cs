using FluentValidation;

namespace BmadPlatform.Application.Features.Initiatives.UpdateInitiative;

public sealed class UpdateInitiativeCommandValidator : AbstractValidator<UpdateInitiativeCommand>
{
    public UpdateInitiativeCommandValidator()
    {
        RuleFor(command => command.Name).ValidInitiativeName();
        RuleFor(command => command.Description).ValidInitiativeDescription();
        this.ValidInitiativeDepthChoice(command => command.DepthMode, command => command.Depth);
    }
}
