using MediatR;

namespace RutaProducto.Application.Features.Context.AddFileAttachment;

/// <summary>
/// Adds a file. <paramref name="DeclaredSize"/> is what the browser announced; the real size is counted while the
/// stream is stored. The stream is never logged.
/// </summary>
public sealed record AddFileAttachmentCommand(
    Guid InitiativeId,
    string FileName,
    string? ContentType,
    long DeclaredSize,
    Stream Content) : IRequest<ContextView>;
