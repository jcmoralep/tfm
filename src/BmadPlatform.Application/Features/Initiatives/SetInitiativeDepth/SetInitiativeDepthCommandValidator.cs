using FluentValidation;

namespace BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;

public sealed class SetInitiativeDepthCommandValidator : AbstractValidator<SetInitiativeDepthCommand>
{
    public SetInitiativeDepthCommandValidator()
    {
        RuleFor(command => command.Depth)
            .IsInEnum().WithMessage("El nivel de profundidad no es válido.");
    }
}
