using MediatR;

namespace BmadPlatform.Application.Features.Context.AddLinkAttachment;

public sealed record AddLinkAttachmentCommand(Guid InitiativeId, string Url) : IRequest<ContextView>;
