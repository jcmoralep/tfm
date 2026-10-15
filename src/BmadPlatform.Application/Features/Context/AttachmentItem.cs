using BmadPlatform.Domain.Context;

namespace BmadPlatform.Application.Features.Context;

/// <summary>One row of the context card. <c>Label</c> is the file name, the note title or the host of a link.</summary>
public sealed record AttachmentItem(
    Guid Id,
    AttachmentKind Kind,
    string Label,
    string? Url,
    string? NoteText,
    long? SizeBytes,
    DateTimeOffset CreatedAt);
