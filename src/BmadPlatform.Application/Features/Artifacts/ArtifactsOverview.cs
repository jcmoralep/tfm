using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// The documents of the initiative's level. <paramref name="Slots"/> is empty while the initiative is a Draft (then
/// <paramref name="Notice"/> says why) or has no level yet. Documents of types outside the level never appear.
/// </summary>
public sealed record ArtifactsOverview(
    Guid InitiativeId,
    IReadOnlyList<ArtifactSlotView> Slots,
    string? Notice,
    bool CompletionPending);

/// <param name="Type">Null for the documents that arrive in a later step.</param>
/// <param name="Availability">Whether the document can be generated now; <c>NotYetSupported</c> when the type is null.</param>
/// <param name="Generated">True when a document exists; then <paramref name="Status"/> and <paramref name="Version"/> are set.</param>
/// <param name="Reason">Plain explanation when the document cannot be generated now.</param>
public sealed record ArtifactSlotView(
    string Label,
    ArtifactType? Type,
    ArtifactAvailability Availability,
    bool Generated,
    ArtifactStatus? Status,
    int Version,
    string? Reason);
