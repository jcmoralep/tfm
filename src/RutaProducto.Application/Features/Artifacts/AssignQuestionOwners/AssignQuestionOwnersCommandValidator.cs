using RutaProducto.Application.Features.Artifacts.Markdown;
using FluentValidation;

namespace RutaProducto.Application.Features.Artifacts.AssignQuestionOwners;

public sealed class AssignQuestionOwnersCommandValidator : AbstractValidator<AssignQuestionOwnersCommand>
{
    private const int MaxAssignments = 200;

    public AssignQuestionOwnersCommandValidator()
    {
        RuleFor(command => command.InitiativeId).NotEmpty().WithMessage(ArtifactValidationTexts.InitiativeRequired);
        RuleFor(command => command.Type).IsInEnum().WithMessage(ArtifactValidationTexts.TypeInvalid);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0).WithMessage(ArtifactValidationTexts.VersionInvalid);

        RuleFor(command => command.Assignments)
            .Cascade(CascadeMode.Stop)
            .Must(list => list is { Count: > 0 }).WithMessage(ArtifactValidationTexts.NoQuestions)
            .Must(list => list.Count <= MaxAssignments).WithMessage(ArtifactValidationTexts.TooManyQuestions)
            .Must(list => list.All(a => a is not null && OpenQuestions.IsValidId(a.Id))).WithMessage(ArtifactValidationTexts.QuestionIdInvalid)
            .Must(list => list.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() == list.Count).WithMessage(ArtifactValidationTexts.DuplicateQuestions)
            .Custom(ValidateValues);
    }

    // The messages never echo the typed values; the row id tells the person which question to fix.
    private static void ValidateValues(IReadOnlyList<QuestionAssignment> assignments, ValidationContext<AssignQuestionOwnersCommand> context)
    {
        foreach (var assignment in assignments)
        {
            Check(context, assignment.Id, assignment.Owner, QuestionOwners.OwnerMaxLength, ArtifactValidationTexts.OwnerTooLong, ArtifactValidationTexts.OwnerForbidden);
            Check(context, assignment.Id, assignment.Revisit, QuestionOwners.RevisitMaxLength, ArtifactValidationTexts.RevisitTooLong, ArtifactValidationTexts.RevisitForbidden);
        }
    }

    private static void Check(ValidationContext<AssignQuestionOwnersCommand> context, string id, string? value, int maxLength, string tooLong, string forbidden)
    {
        if (!QuestionOwners.IsAllowed(value))
        {
            context.AddFailure(nameof(AssignQuestionOwnersCommand.Assignments), $"{id}: {forbidden}");
        }
        else if (QuestionOwners.Clean(value).Length > maxLength)
        {
            context.AddFailure(nameof(AssignQuestionOwnersCommand.Assignments), $"{id}: {tooLong}");
        }
    }
}
