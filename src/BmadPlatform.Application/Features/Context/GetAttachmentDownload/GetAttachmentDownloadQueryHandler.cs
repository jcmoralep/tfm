using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Abstractions.Storage;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Domain.Context;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Features.Context.GetAttachmentDownload;

public sealed class GetAttachmentDownloadQueryHandler(
    ISender sender,
    IAttachmentRepository repository,
    IFileStorage storage,
    ICurrentUser currentUser,
    ILogger<GetAttachmentDownloadQueryHandler> logger) : IRequestHandler<GetAttachmentDownloadQuery, AttachmentDownload?>
{
    public async Task<AttachmentDownload?> Handle(GetAttachmentDownloadQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        if (await sender.Send(new GetInitiativeQuery(request.InitiativeId), cancellationToken) is null)
        {
            return null;
        }

        var attachment = await repository.GetAsync(request.AttachmentId, request.InitiativeId, ownerId, cancellationToken);

        if (attachment is not { Kind: AttachmentKind.File, StorageKey: { } key, Name: { } name })
        {
            return null;
        }

        var content = await storage.OpenReadAsync(key, cancellationToken);

        if (content is null)
        {
            logger.LogWarning(
                "The stored file is missing for initiative {InitiativeId}, attachment {AttachmentId}.",
                request.InitiativeId,
                request.AttachmentId);

            return null;
        }

        return new AttachmentDownload(name, content);
    }
}
