using FluentValidation;

namespace RutaProducto.Application.Features.Artifacts.GenerateArtifact;

public sealed class GenerateArtifactCommandValidator : AbstractValidator<GenerateArtifactCommand>
{
    public GenerateArtifactCommandValidator()
    {
        RuleFor(command => command.InitiativeId).NotEmpty().WithMessage(ArtifactValidationTexts.InitiativeRequired);
        RuleFor(command => command.Type).IsInEnum().WithMessage(ArtifactValidationTexts.TypeInvalid);
    }
}
