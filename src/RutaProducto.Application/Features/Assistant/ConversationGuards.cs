using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Application.Features.Initiatives.GetInitiative;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Initiatives;
using MediatR;

namespace RutaProducto.Application.Features.Assistant;

/// <summary>Checks shared by the conversation commands, so each rule is worded and enforced once.</summary>
internal static class ConversationGuards
{
    /// <summary>The initiative of the current user; the same not-found whether it is foreign, deleted or unknown.</summary>
    public static async Task<InitiativeDetails> GetInitiativeAsync(ISender sender, Guid initiativeId, CancellationToken cancellationToken) =>
        await sender.Send(new GetInitiativeQuery(initiativeId), cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

    /// <summary>Start, send and undo are only possible while the initiative is Clarifying or Planning.</summary>
    public static void RequireOpen(InitiativeStatus status)
    {
        switch (status)
        {
            case InitiativeStatus.Draft:
                throw new DomainException(AssistantTexts.DraftNotAllowed);
            case InitiativeStatus.ReadyToBuild:
                throw new DomainException(AssistantTexts.ReadyToBuildReadOnly);
        }
    }
}
