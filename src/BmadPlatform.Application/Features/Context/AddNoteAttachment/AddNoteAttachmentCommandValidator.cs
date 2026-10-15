using BmadPlatform.Domain.Context;
using FluentValidation;

namespace BmadPlatform.Application.Features.Context.AddNoteAttachment;

public sealed class AddNoteAttachmentCommandValidator : AbstractValidator<AddNoteAttachmentCommand>
{
    public AddNoteAttachmentCommandValidator()
    {
        RuleFor(command => command.InitiativeId).NotEmpty();

        RuleFor(command => command.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage(Attachment.EmptyNoteTitleMessage)
            .Must(title => (title?.Trim().Length ?? 0) <= Attachment.NoteTitleMaxLength).WithMessage(Attachment.NoteTitleTooLongMessage);

        RuleFor(command => command.Text)
            .Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage(Attachment.EmptyNoteTextMessage)
            .Must(text => (text?.Trim().Length ?? 0) <= Attachment.NoteTextMaxLength).WithMessage(Attachment.NoteTextTooLongMessage);
    }
}
