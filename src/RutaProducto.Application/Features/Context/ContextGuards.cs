using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Application.Features.Initiatives.GetInitiative;
using MediatR;

namespace RutaProducto.Application.Features.Context;

/// <summary>Checks shared by the context use cases. Context never touches initiatives except through their query.</summary>
internal static class ContextGuards
{
    /// <summary>
    /// The initiative of the current user, in any status (RN-04); the same not-found whether it is foreign, deleted or
    /// unknown.
    /// </summary>
    public static async Task<InitiativeDetails> GetInitiativeAsync(ISender sender, Guid initiativeId, CancellationToken cancellationToken) =>
        await sender.Send(new GetInitiativeQuery(initiativeId), cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);
}
