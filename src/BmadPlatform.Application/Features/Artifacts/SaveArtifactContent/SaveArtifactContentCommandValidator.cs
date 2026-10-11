using BmadPlatform.Domain.Artifacts;
using FluentValidation;

namespace BmadPlatform.Application.Features.Artifacts.SaveArtifactContent;

public sealed class SaveArtifactContentCommandValidator : AbstractValidator<SaveArtifactContentCommand>
{
    public SaveArtifactContentCommandValidator()
    {
        RuleFor(command => command.InitiativeId).NotEmpty().WithMessage(ArtifactValidationTexts.InitiativeRequired);
        RuleFor(command => command.Type).IsInEnum().WithMessage(ArtifactValidationTexts.TypeInvalid);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0).WithMessage(ArtifactValidationTexts.VersionInvalid);

        // The cap is measured on the stored text, after line endings are unified and the edges trimmed.
        RuleFor(command => Artifact.Normalize(command.Content))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(Artifact.EmptyContentMessage)
            .MaximumLength(Artifact.ContentMaxLength).WithMessage(Artifact.TooLongMessage)
            .OverridePropertyName(nameof(SaveArtifactContentCommand.Content));
    }
}
