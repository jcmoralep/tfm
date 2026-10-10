using FluentValidation;

namespace BmadPlatform.Application.Features.Artifacts.ApproveArtifact;

public sealed class ApproveArtifactCommandValidator : AbstractValidator<ApproveArtifactCommand>
{
    public ApproveArtifactCommandValidator()
    {
        RuleFor(command => command.InitiativeId).NotEmpty().WithMessage(ArtifactValidationTexts.InitiativeRequired);
        RuleFor(command => command.Type).IsInEnum().WithMessage(ArtifactValidationTexts.TypeInvalid);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0).WithMessage(ArtifactValidationTexts.VersionInvalid);
    }
}
