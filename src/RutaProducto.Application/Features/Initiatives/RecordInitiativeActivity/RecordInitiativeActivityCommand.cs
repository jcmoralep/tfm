using MediatR;

namespace RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;

/// <summary>
/// Notes that the person worked on the initiative (a message, a document, an attachment) without changing the
/// initiative itself. Other modules send it through <see cref="InitiativeActivity.RecordActivityAsync"/>, never directly.
/// </summary>
public sealed record RecordInitiativeActivityCommand(Guid InitiativeId) : IRequest
{
    // Only the name, like the other commands that must not print their data.
    public override string ToString() => nameof(RecordInitiativeActivityCommand);
}
