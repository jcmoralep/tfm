using MediatR;

namespace RutaProducto.Application.Features.Context.AddNoteAttachment;

public sealed record AddNoteAttachmentCommand(Guid InitiativeId, string Title, string Text) : IRequest<ContextView>;
