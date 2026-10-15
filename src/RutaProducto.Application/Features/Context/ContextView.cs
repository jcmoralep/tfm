using RutaProducto.Domain.Context;

namespace RutaProducto.Application.Features.Context;

/// <summary>The context of an initiative as the card shows it, with the limits the card needs to enforce.</summary>
public sealed record ContextView(IReadOnlyList<AttachmentItem> Items, long TotalBytes, ContextLimits Limits)
{
    public bool CanAdd => Items.Count < ContextLimits.MaxItems;

    /// <summary>Builds the view from the stored rows, keeping the order given (oldest first).</summary>
    public static ContextView From(IReadOnlyList<Attachment> attachments, ContextLimits limits) =>
        new(
            [.. attachments.Select(ToItem)],
            attachments.Sum(attachment => attachment.SizeBytes ?? 0),
            limits);

    private static AttachmentItem ToItem(Attachment attachment) => new(
        attachment.Id,
        attachment.Kind,
        attachment.Kind == AttachmentKind.Link ? new Uri(attachment.Url!).Host : attachment.Name ?? string.Empty,
        attachment.Url,
        attachment.Text,
        attachment.SizeBytes,
        attachment.CreatedAt);
}
