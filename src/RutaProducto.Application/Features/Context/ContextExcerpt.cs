using RutaProducto.Domain.Context;

namespace RutaProducto.Application.Features.Context;

/// <summary>
/// The part of one text-like item that may go to the model: a note, or a text file read as UTF-8. It carries the
/// title or file name and the text only (no ids, no URLs); it is a DTO that other modules are allowed to reference.
/// </summary>
public sealed record ContextExcerpt(AttachmentKind Kind, string Title, string Text);
