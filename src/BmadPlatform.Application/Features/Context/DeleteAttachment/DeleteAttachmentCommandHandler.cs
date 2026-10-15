using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Abstractions.Storage;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Context;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Features.Context.DeleteAttachment;

public sealed class DeleteAttachmentCommandHandler(
    ISender sender,
    IAttachmentRepository repository,
    IFileStorage storage,
    ICurrentUser currentUser,
    ContextLimits limits,
    ILogger<DeleteAttachmentCommandHandler> logger) : IRequestHandler<DeleteAttachmentCommand, ContextView>
{
    public async Task<ContextView> Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ContextGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);

        var attachment = await repository.GetAsync(request.AttachmentId, initiative.Id, ownerId, cancellationToken)
            ?? throw new NotFoundException(ContextTexts.NotFound);

        // The file goes first. If it cannot be removed the bytes become an orphan, which is logged and tolerated:
        // the person asked for the item to disappear and a row without a file is worse than a file without a row.
        if (attachment.Kind == AttachmentKind.File && attachment.StorageKey is { } key)
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    "A stored file was left without a row for initiative {InitiativeId}, attachment {AttachmentId} ({ExceptionType}).",
                    initiative.Id,
                    attachment.Id,
                    exception.GetType().Name);
            }
        }

        if (!await repository.DeleteAsync(attachment.Id, ownerId, cancellationToken))
        {
            throw new NotFoundException(ContextTexts.NotFound);
        }

        return ContextView.From(await repository.ListAsync(initiative.Id, ownerId, cancellationToken), limits);
    }
}
