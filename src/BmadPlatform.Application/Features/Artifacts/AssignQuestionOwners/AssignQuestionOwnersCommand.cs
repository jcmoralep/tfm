using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Domain.Artifacts;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts.AssignQuestionOwners;

/// <summary>
/// Writes the owner and the revisit condition of open questions into the document in one save. It is an edit:
/// <paramref name="ExpectedVersion"/> is the version the page was showing, a stale one is rejected, and storing a
/// change raises the version and returns the document to Draft. The same values again are a no-op.
/// </summary>
public sealed record AssignQuestionOwnersCommand(
    Guid InitiativeId,
    ArtifactType Type,
    int ExpectedVersion,
    IReadOnlyList<QuestionAssignment> Assignments) : IRequest<ArtifactView>
{
    // The synthesized ToString would print the owners and revisit texts typed by the user.
    public override string ToString() =>
        $"{nameof(AssignQuestionOwnersCommand)} {{ InitiativeId = {InitiativeId}, Type = {Type}, " +
        $"ExpectedVersion = {ExpectedVersion}, Assignments = {Assignments?.Count ?? 0} [redacted] }}";
}
