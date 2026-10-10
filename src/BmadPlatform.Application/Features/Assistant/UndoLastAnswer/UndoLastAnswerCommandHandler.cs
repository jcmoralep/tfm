using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using MediatR;

namespace BmadPlatform.Application.Features.Assistant.UndoLastAnswer;

public sealed class UndoLastAnswerCommandHandler(
    ISender sender,
    IConversationRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<UndoLastAnswerCommand, ConversationView>
{
    public async Task<ConversationView> Handle(UndoLastAnswerCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ConversationGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);
        ConversationGuards.RequireOpen(initiative.Status);

        var conversation = await repository.GetAsync(initiative.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(AssistantTexts.ConversationNotStarted);

        if (conversation.Version != request.ExpectedVersion)
        {
            throw new ConflictException();
        }

        conversation.UndoLastAnswer(timeProvider.GetUtcNow());
        await repository.SaveAsync(conversation, request.ExpectedVersion, cancellationToken);

        return ConversationViewBuilder.Build(initiative, conversation);
    }
}
