using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Context;
using RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Features.Context.AddNoteAttachment;

public sealed class AddNoteAttachmentCommandHandler(
    ISender sender,
    IAttachmentRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ContextLimits limits,
    ILogger<AddNoteAttachmentCommandHandler> logger) : IRequestHandler<AddNoteAttachmentCommand, ContextView>
{
    public async Task<ContextView> Handle(AddNoteAttachmentCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await ContextGuards.GetInitiativeAsync(sender, request.InitiativeId, cancellationToken);

        var (count, _) = await repository.UsageAsync(initiative.Id, ownerId, cancellationToken);

        if (count >= ContextLimits.MaxItems)
        {
            throw new DomainException(ContextTexts.TooManyItems);
        }

        await repository.AddAsync(
            Attachment.CreateNote(initiative.Id, ownerId, request.Title, request.Text, timeProvider.GetUtcNow()),
            cancellationToken);

        await sender.RecordActivityAsync(logger, initiative.Id, cancellationToken);

        return ContextView.From(await repository.ListAsync(initiative.Id, ownerId, cancellationToken), limits);
    }
}
