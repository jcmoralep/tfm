using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Storage;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Context;
using RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Features.Context.AddFileAttachment;

public sealed class AddFileAttachmentCommandHandler(
    ISender sender,
    IAttachmentRepository repository,
    IFileStorage storage,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ContextLimits limits,
    ILogger<AddFileAttachmentCommandHandler> logger) : IRequestHandler<AddFileAttachmentCommand, ContextView>
{
    public async Task<ContextView> Handle(AddFileAttachmentCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ContextGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);

        var (count, totalBytes) = await repository.UsageAsync(initiative.Id, ownerId, cancellationToken);

        if (count >= ContextLimits.MaxItems)
        {
            throw new DomainException(ContextTexts.TooManyItems);
        }

        if (totalBytes + request.DeclaredSize > ContextLimits.MaxTotalBytes)
        {
            throw new DomainException(ContextTexts.TotalTooLarge);
        }

        var id = Guid.NewGuid();
        var key = Attachment.StorageKeyFor(initiative.Id, id);
        var remaining = ContextLimits.MaxTotalBytes - totalBytes;

        long size;

        try
        {
            size = await storage.SaveAsync(key, request.Content, Math.Min(limits.MaxFileBytes, remaining), cancellationToken);
        }
        catch (FileTooLargeException)
        {
            // The size announced by the browser was wrong: the cap that was hit decides which message applies.
            throw new DomainException(limits.MaxFileBytes <= remaining
                ? ContextTexts.FileTooLarge(limits.MaxFileBytes)
                : ContextTexts.TotalTooLarge);
        }

        try
        {
            var attachment = Attachment.CreateFile(
                id, initiative.Id, ownerId, request.FileName, request.ContentType, size, timeProvider.GetUtcNow());

            await repository.AddAsync(attachment, cancellationToken);
        }
        catch
        {
            await RemoveStoredFileAsync(key, initiative.Id, id);

            throw;
        }

        await sender.RecordActivityAsync(logger, initiative.Id, cancellationToken);

        return ContextView.From(await repository.ListAsync(initiative.Id, ownerId, cancellationToken), limits);
    }

    // Best effort and never cancelled: the row was not created, so the bytes would be orphaned.
    private async Task RemoveStoredFileAsync(string key, Guid initiativeId, Guid attachmentId)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "A stored file was left without a row for initiative {InitiativeId}, attachment {AttachmentId} ({ExceptionType}).",
                initiativeId,
                attachmentId,
                exception.GetType().Name);
        }
    }
}
