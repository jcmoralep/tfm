using MediatR;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;

/// <summary>How the other modules note activity on an initiative: through MediatR, and never at the cost of their own action.</summary>
public static class InitiativeActivity
{
    /// <summary>
    /// Sends <see cref="RecordInitiativeActivityCommand"/> after the main action succeeded. Best effort: any failure
    /// (cancellation excepted) is logged as a warning with the initiative id and the exception type only, and swallowed.
    /// </summary>
    public static async Task RecordActivityAsync(
        this ISender sender,
        ILogger logger,
        Guid initiativeId,
        CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new RecordInitiativeActivityCommand(initiativeId), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Recording activity failed for initiative {InitiativeId} ({ExceptionType}).",
                initiativeId,
                exception.GetType().Name);
        }
    }
}
