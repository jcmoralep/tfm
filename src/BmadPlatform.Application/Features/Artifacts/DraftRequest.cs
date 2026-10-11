using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// What a drafter sees. It carries text and level data only: no ids, owner, e-mail or timestamps, so nothing
/// that identifies a person or a record can leave the process with it.
/// </summary>
public sealed record DraftRequest(
    ArtifactType Type,
    string InitiativeName,
    string? Description,
    InitiativeDepth Depth,
    IReadOnlyList<TopicAnswer> Answers);
