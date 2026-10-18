using System.Diagnostics;
using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Documents;
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
    IEnumerable<IDocumentTextExtractor> extractors,
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

            await ExtractTextAsync(attachment, key, cancellationToken);

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

    // Best effort: the original file is already stored, so no failure, timeout or odd content here may fail the upload.
    // Only the user cancelling the request is passed on. Logs carry counts and the exception type, never name or text.
    private async Task ExtractTextAsync(Attachment attachment, string key, CancellationToken cancellationToken)
    {
        var extension = AttachmentFileName.ExtensionOf(attachment.Name ?? string.Empty);
        var extractor = extractors.FirstOrDefault(candidate => candidate.Supports(extension));

        if (extractor is null)
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        var result = DocumentTextResult.Unreadable;
        string? failure = null;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(limits.ExtractionTimeout);

            await using var content = await storage.OpenReadAsync(key, timeout.Token);

            if (content is not null)
            {
                // Task.Run keeps an extractor that blocks its thread from freezing the request, and WaitAsync bounds one that ignores its token.
                result = await Task.Run(() => extractor.ExtractAsync(content, timeout.Token), timeout.Token).WaitAsync(limits.ExtractionTimeout, cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            failure = exception.GetType().Name;
        }

        if (result.Status == ExtractionStatus.Extracted && string.IsNullOrWhiteSpace(result.Markdown))
        {
            result = DocumentTextResult.Unreadable;
        }

        attachment.ApplyExtraction(result.Status, result.Markdown);

        logger.LogInformation(
            "Text extraction for initiative {InitiativeId}, attachment {AttachmentId}: {Status}, {Characters} characters, {ElapsedMs} ms{Failure}.",
            attachment.InitiativeId,
            attachment.Id,
            result.Status,
            result.Markdown?.Length ?? 0,
            clock.ElapsedMilliseconds,
            failure is null ? string.Empty : $", failed with {failure}");
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
