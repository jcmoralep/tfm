using FluentValidation;

namespace BmadPlatform.Application.Features.Initiatives.SaveInitiativeDepth;

public sealed class SaveInitiativeDepthCommandValidator : AbstractValidator<SaveInitiativeDepthCommand>
{
    public SaveInitiativeDepthCommandValidator()
    {
        this.ValidInitiativeDepthChoice(command => command.DepthMode, command => command.Depth);
    }
}
