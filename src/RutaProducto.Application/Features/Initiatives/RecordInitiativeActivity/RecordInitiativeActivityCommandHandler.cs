using RutaProducto.Application.Abstractions.Authentication;
using MediatR;

namespace RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;

/// <summary>
/// Stores "last activity" on the caller's own initiative. Someone else's, deleted or unknown initiatives are ignored
/// without an error, so the command reveals nothing. A recent activity (under a minute) is not written again: a chat
/// of many messages costs one write a minute, not one per message. The check and the write are one atomic statement
/// that touches only the activity date, so a concurrent rename or state change is never reverted.
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

        await repository.RecordActivityAsync(
            ownerId,
            request.InitiativeId,
            timeProvider.GetUtcNow(),
            DebounceWindow,
            cancellationToken);
    }
}
