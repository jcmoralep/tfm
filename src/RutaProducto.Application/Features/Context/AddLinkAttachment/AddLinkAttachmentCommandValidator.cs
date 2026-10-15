using RutaProducto.Domain.Context;
using FluentValidation;

namespace RutaProducto.Application.Features.Context.AddLinkAttachment;

public sealed class AddLinkAttachmentCommandValidator : AbstractValidator<AddLinkAttachmentCommand>
{
    public AddLinkAttachmentCommandValidator()
    {
        RuleFor(command => command.InitiativeId).NotEmpty();

        RuleFor(command => command.Url)
            .Must(url => (url?.Trim().Length ?? 0) <= Attachment.UrlMaxLength)
            .WithMessage(Attachment.UrlTooLongMessage)
            .DependentRules(() =>
                RuleFor(command => command.Url)
                    .Must(Attachment.IsAllowedUrl)
                    .WithMessage(Attachment.InvalidUrlMessage));
    }
}
