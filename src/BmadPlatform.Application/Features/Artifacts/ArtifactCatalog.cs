using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// One document of a level's required set. <paramref name="Type"/> is null for documents that arrive in a later
/// step (Architecture, Epics and stories), which are listed but can never be generated or approved yet.
/// </summary>
public sealed record ArtifactSlot(string Label, ArtifactType? Type);

public enum ArtifactAvailability
{
    Available,
    InitiativeDraft,
    LevelNotSet,
    NotRequired,
    WaitingForClarify,
    WaitingForClosing,
    NotYetSupported,
}

/// <summary>Which documents each level requires and when each one can be generated. Pure data and rules.</summary>
public static class ArtifactCatalog
{
    private static readonly ArtifactSlot[] Small = [new("Especificación", ArtifactType.Spec)];

    private static readonly ArtifactSlot[] Standard = [new("Brief", ArtifactType.Brief), new("PRD", ArtifactType.Prd)];

    private static readonly ArtifactSlot[] Large =
    [
        new("PRD", ArtifactType.Prd),
        new("Arquitectura", null),
        new("Épicas e historias", null),
    ];

    /// <summary>The slots of the level in delivery order. Empty while no level is set.</summary>
    public static IReadOnlyList<ArtifactSlot> RequiredFor(InitiativeDepth? depth) => depth switch
    {
        InitiativeDepth.Small => Small,
        InitiativeDepth.Standard => Standard,
        InitiativeDepth.Large => Large,
        _ => [],
    };

    /// <summary>
    /// Whether a document can be generated now. The answers carry the coverage flags computed by the assistant;
    /// missing answers count as nothing covered.
    /// </summary>
    public static ArtifactAvailability Availability(
        ArtifactType type,
        InitiativeStatus status,
        InitiativeDepth? depth,
        ConversationAnswers? answers)
    {
        if (status == InitiativeStatus.Draft)
        {
            return ArtifactAvailability.InitiativeDraft;
        }

        if (depth is null)
        {
            return ArtifactAvailability.LevelNotSet;
        }

        if (!RequiredFor(depth).Any(slot => slot.Type == type))
        {
            return ArtifactAvailability.NotRequired;
        }

        if (type == ArtifactType.Brief)
        {
            return answers is { ClarifyComplete: true }
                ? ArtifactAvailability.Available
                : ArtifactAvailability.WaitingForClarify;
        }

        // Spec and PRD wait for the closing: Planning (or later) with every required topic covered.
        var planning = status is InitiativeStatus.Planning or InitiativeStatus.ReadyToBuild;

        return planning && answers is { PlanComplete: true }
            ? ArtifactAvailability.Available
            : ArtifactAvailability.WaitingForClosing;
    }

    /// <summary>True when every slot of the level has a type and its document is approved.</summary>
    public static bool IsSetComplete(InitiativeDepth? depth, IReadOnlyList<ArtifactSummary> summaries)
    {
        var slots = RequiredFor(depth);

        if (slots.Count == 0 || slots.Any(slot => slot.Type is null))
        {
            return false;
        }

        return slots.All(slot => summaries.Any(s => s.Type == slot.Type && s.Status == ArtifactStatus.Approved));
    }
}
