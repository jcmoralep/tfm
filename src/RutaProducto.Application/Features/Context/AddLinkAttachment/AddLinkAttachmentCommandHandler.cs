using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Context;
using RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Features.Context.AddLinkAttachment;

public sealed class AddLinkAttachmentCommandHandler(
    ISender sender,
    IAttachmentRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ContextLimits limits,
    ILogger<AddLinkAttachmentCommandHandler> logger) : IRequestHandler<AddLinkAttachmentCommand, ContextView>
{
    public async Task<ContextView> Handle(AddLinkAttachmentCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ContextGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);

        var (count, _) = await repository.UsageAsync(initiative.Id, ownerId, cancellationToken);

        if (count >= ContextLimits.MaxItems)
        {
            throw new DomainException(ContextTexts.TooManyItems);
        }

        await repository.AddAsync(
            Attachment.CreateLink(initiative.Id, ownerId, request.Url, timeProvider.GetUtcNow()), cancellationToken);

        await sender.RecordActivityAsync(logger, initiative.Id, cancellationToken);

        return ContextView.From(await repository.ListAsync(initiative.Id, ownerId, cancellationToken), limits);
    }
}
