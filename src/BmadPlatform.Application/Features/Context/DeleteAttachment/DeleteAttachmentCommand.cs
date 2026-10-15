using MediatR;

namespace BmadPlatform.Application.Features.Context.DeleteAttachment;

public sealed record DeleteAttachmentCommand(Guid InitiativeId, Guid AttachmentId) : IRequest<ContextView>;
