using System.Text;
using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Storage;
using RutaProducto.Application.Features.Initiatives.GetInitiative;
using RutaProducto.Domain.Context;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Features.Context.GetContextForAssistant;

/// <summary>
/// Selects the notes, the text files (by normalized extension, never by content type) and the files whose text the
/// server already extracted (their stored Markdown is the content). Reads at most <see cref="MaxFileBytes"/> of each
/// text file as UTF-8 with replacement, and applies the caps. A file that cannot be
/// read is skipped. Only counts are logged: no title, name or text.
/// </summary>
public sealed class GetContextForAssistantQueryHandler(
    ISender sender,
    IAttachmentRepository repository,
    IFileStorage storage,
    ICurrentUser currentUser,
    ILogger<GetContextForAssistantQueryHandler> logger) : IRequestHandler<GetContextForAssistantQuery, IReadOnlyList<ContextExcerpt>>
{
    public const int MaxFileBytes = 20_000;

    public const int MaxItemCharacters = 15_000;

    public const int MaxTotalCharacters = 60_000;

    public const int MaxItems = 10;

    public const string ClippedMarker = "[recortado]";

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".markdown", ".csv" };

    public async Task<IReadOnlyList<ContextExcerpt>> Handle(GetContextForAssistantQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        if (await sender.Send(new GetInitiativeQuery(request.InitiativeId), cancellationToken) is null)
        {
            return [];
        }

        var candidates = (await repository.ListAsync(request.InitiativeId, ownerId, cancellationToken))
            .Where(IsTextLike)
            .ToList();
        var excerpts = new List<ContextExcerpt>();
        var characters = 0;

        foreach (var attachment in candidates)
        {
            var remaining = MaxTotalCharacters - characters;

            if (excerpts.Count >= MaxItems || remaining < ClippedMarker.Length + 1)
            {
                break;
            }

            var text = await ReadTextAsync(attachment, request.InitiativeId, cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            text = Clip(text.Trim(), Math.Min(MaxItemCharacters, remaining));
            excerpts.Add(new ContextExcerpt(attachment.Kind, attachment.Name ?? string.Empty, text));
            characters += text.Length;
        }

        logger.LogInformation(
            "Context for the assistant of initiative {InitiativeId}: {ItemsSent} items sent, {ItemsSkipped} skipped, {CharactersSent} characters.",
            request.InitiativeId,
            excerpts.Count,
            candidates.Count - excerpts.Count,
            characters);

        return excerpts;
    }

    private static bool IsTextLike(Attachment attachment) =>
        attachment.Kind == AttachmentKind.Note
        || IsExtracted(attachment)
        || (attachment.Kind == AttachmentKind.File
            && attachment.StorageKey is not null
            && TextExtensions.Contains(AttachmentFileName.ExtensionOf(attachment.Name ?? string.Empty)));

    private static bool IsExtracted(Attachment attachment) =>
        attachment.Kind == AttachmentKind.File && attachment.ExtractionStatus == ExtractionStatus.Extracted;

    // The clipped text ends with the marker and the whole thing stays within the limit.
    private static string Clip(string text, int limit) =>
        text.Length <= limit ? text : text[..(limit - ClippedMarker.Length)] + ClippedMarker;

    private async Task<string?> ReadTextAsync(Attachment attachment, Guid initiativeId, CancellationToken cancellationToken)
    {
        if (attachment.Kind == AttachmentKind.Note)
        {
            return attachment.Text;
        }

        if (IsExtracted(attachment))
        {
            return attachment.ExtractedText;
        }

        try
        {
            await using var content = await storage.OpenReadAsync(attachment.StorageKey!, cancellationToken);

            if (content is null)
            {
                logger.LogWarning(
                    "The stored file is missing for initiative {InitiativeId}, attachment {AttachmentId}.",
                    initiativeId,
                    attachment.Id);

                return null;
            }

            var buffer = new byte[MaxFileBytes];
            var length = 0;
            int read;

            while (length < buffer.Length && (read = await content.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
            {
                length += read;
            }

            return Encoding.UTF8.GetString(buffer, 0, length).TrimStart('﻿');
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "The stored file could not be read for initiative {InitiativeId}, attachment {AttachmentId} ({ExceptionType}).",
                initiativeId,
                attachment.Id,
                exception.GetType().Name);

            return null;
        }
    }
}
