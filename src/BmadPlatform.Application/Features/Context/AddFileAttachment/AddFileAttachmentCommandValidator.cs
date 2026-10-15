using BmadPlatform.Domain.Context;
using FluentValidation;

namespace BmadPlatform.Application.Features.Context.AddFileAttachment;

public sealed class AddFileAttachmentCommandValidator : AbstractValidator<AddFileAttachmentCommand>
{
    public AddFileAttachmentCommandValidator(ContextLimits limits)
    {
        RuleFor(command => command.InitiativeId).NotEmpty();

        RuleFor(command => command.Content).NotNull();

        RuleFor(command => command.FileName)
            .Must(name => !Attachment.IsBlockedExtension(name))
            .WithMessage(Attachment.BlockedFileMessage);

        RuleFor(command => command.DeclaredSize)
            .GreaterThan(0).WithMessage(Attachment.EmptyFileMessage)
            .LessThanOrEqualTo(limits.MaxFileBytes).WithMessage(ContextTexts.FileTooLarge(limits.MaxFileBytes));
    }
}
