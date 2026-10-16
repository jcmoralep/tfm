using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Common.Exceptions;
using MediatR;

namespace RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;

/// <summary>
/// Stores "last activity" on the caller's own initiative. Someone else's, deleted or unknown initiatives are ignored
/// without an error, so the command reveals nothing. A recent activity (under a minute) is not written again: a chat
/// of many messages costs one write a minute, not one per message.
/// </summary>
public sealed class RecordInitiativeActivityCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<RecordInitiativeActivityCommand>
{
    public static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(60);

    public async Task Handle(RecordInitiativeActivityCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        var initiative = await repository.GetAsync(request.InitiativeId, ownerId, cancellationToken);

        if (initiative is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        if (now - initiative.EffectiveActivityAt < DebounceWindow)
        {
            return;
        }

        initiative.RecordActivity(now);

        try
        {
            await repository.UpdateAsync(initiative, cancellationToken);
        }
        catch (NotFoundException)
        {
            // Deleted between the read and the write: nothing left to note.
        }
    }
}
