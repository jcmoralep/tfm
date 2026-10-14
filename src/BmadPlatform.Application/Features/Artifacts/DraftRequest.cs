using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// What a drafter sees. It carries text and level data only: no ids, owner, e-mail or timestamps, so nothing
/// that identifies a person or a record can leave the process with it. <c>BriefMarkdown</c> is the text of the approved
/// Brief and is set only for the PRD of a Standard initiative whose Brief is Approved; the fake drafter ignores it.
/// <c>PrdMarkdown</c> is the text of the approved PRD, set for the Architecture and the Epics and stories while the PRD is
/// Approved; <c>ArchitectureMarkdown</c> is the approved Architecture, set for the Epics and stories only. Those two
/// documents carry no conversation answers: their source is the approved upstream text.
/// </summary>
public sealed record DraftRequest(
    ArtifactType Type,
    string InitiativeName,
    string? Description,
    InitiativeDepth Depth,
    IReadOnlyList<TopicAnswer> Answers,
    string? BriefMarkdown = null,
    string? PrdMarkdown = null,
    string? ArchitectureMarkdown = null);
