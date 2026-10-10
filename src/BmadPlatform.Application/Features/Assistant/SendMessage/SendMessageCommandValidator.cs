using BmadPlatform.Domain.Assistant;
using FluentValidation;

namespace BmadPlatform.Application.Features.Assistant.SendMessage;

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        // Text and key are exclusive. A blank text counts as absent, so a button click with an empty box is valid.
        RuleFor(command => command)
            .Must(command => string.IsNullOrWhiteSpace(command.Text) || string.IsNullOrWhiteSpace(command.QuickReplyKey))
            .WithName(nameof(SendMessageCommand.Text))
            .WithMessage(AssistantTexts.AnswerOrOptionOnly)
            .DependentRules(() =>
            {
                RuleFor(command => command.Text)
                    .Must(text => text is null || text.Trim().Length <= Conversation.UserAnswerMaxLength)
                    .WithMessage(AssistantTexts.AnswerTooLong);

                RuleFor(command => command)
                    .Must(command => !string.IsNullOrWhiteSpace(command.Text) || !string.IsNullOrWhiteSpace(command.QuickReplyKey))
                    .WithName(nameof(SendMessageCommand.Text))
                    .WithMessage(AssistantTexts.EmptyAnswer);
            });

        RuleFor(command => command.QuickReplyKey)
            .Must(key => key is null || key.Length <= Message.KeyMaxLength)
            .WithMessage(AssistantTexts.InvalidQuickReply);
    }
}
