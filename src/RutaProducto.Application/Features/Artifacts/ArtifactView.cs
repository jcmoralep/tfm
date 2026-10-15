using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Features.Artifacts;

/// <summary>
/// One document as the page needs it. A document that was not generated is a normal result: <see cref="Generated"/>
/// is false, there is no content and the flags say whether it can be generated. <see cref="Version"/> is zero then.
/// The <c>Can*</c> flags are the single source of what the owner may do; the handlers enforce the same rules.
/// </summary>
/// <param name="ApprovalCompletesSet">Approving this draft would make the initiative Ready to build.</param>
/// <param name="CompletionPending">
/// The required set is approved but the initiative is still Planning (a failed transition); resending the approval
/// completes it.
/// </param>
/// <param name="Questions">The open questions read from the Markdown, in document order.</param>
/// <param name="UnassignedQuestions">The distinct ids still to assign; while any remain the draft cannot be approved.</param>
/// <param name="StaleUpstream">
/// The document this one depends on that changed after this one was drafted. Advisory only: nothing is blocked.
/// </param>
public sealed record ArtifactView(
    Guid InitiativeId,
    string InitiativeName,
    InitiativeStatus InitiativeStatus,
    InitiativeDepth? Depth,
    ArtifactType Type,
    bool Generated,
    ArtifactAvailability Availability,
    int Version,
    ArtifactStatus? Status,
    string Content,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ApprovedAt,
    IReadOnlyList<FormatWarning> Warnings,
    bool CanGenerate,
    bool CanEdit,
    bool CanRegenerate,
    bool CanApprove,
    bool ApprovalCompletesSet,
    bool CompletionPending,
    IReadOnlyList<OpenQuestion> Questions,
    IReadOnlyList<string> UnassignedQuestions,
    ArtifactType? StaleUpstream = null)
{
    // The synthesized ToString would print the document, which can contain initiative information.
    public override string ToString() =>
        $"{nameof(ArtifactView)} {{ InitiativeId = {InitiativeId}, Type = {Type}, Version = {Version}, Status = {Status}, Content = [redacted] }}";
}
