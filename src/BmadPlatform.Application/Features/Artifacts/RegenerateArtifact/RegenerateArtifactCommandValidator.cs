using FluentValidation;

namespace BmadPlatform.Application.Features.Artifacts.RegenerateArtifact;

public sealed class RegenerateArtifactCommandValidator : AbstractValidator<RegenerateArtifactCommand>
{
    public RegenerateArtifactCommandValidator()
    {
        RuleFor(command => command.InitiativeId).NotEmpty().WithMessage(ArtifactValidationTexts.InitiativeRequired);
        RuleFor(command => command.Type).IsInEnum().WithMessage(ArtifactValidationTexts.TypeInvalid);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0).WithMessage(ArtifactValidationTexts.VersionInvalid);
    }
}
