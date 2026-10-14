using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// One document of a level's required set. <paramref name="Type"/> is null for documents that arrive in a later
/// step (Epics and stories), which is listed but can never be generated or approved yet.
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
    WaitingForPrd,
    WaitingForArchitecture,
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
        new("Arquitectura", ArtifactType.Architecture),
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

    /// <summary>The documents that must be approved before this one can be drafted, in order. Empty for the first documents.</summary>
    public static IReadOnlyList<ArtifactType> Upstream(ArtifactType type) => type switch
    {
        ArtifactType.Architecture => [ArtifactType.Prd],
        ArtifactType.EpicsAndStories => [ArtifactType.Prd, ArtifactType.Architecture],
        _ => [],
    };

    /// <summary>
    /// Whether a document can be generated now. The answers carry the coverage flags computed by the assistant;
    /// missing answers count as nothing covered. After the coverage holds, every upstream document must be approved.
    /// </summary>
    public static ArtifactAvailability Availability(
        ArtifactType type,
        InitiativeStatus status,
        InitiativeDepth? depth,
        ConversationAnswers? answers,
        IReadOnlyList<ArtifactSummary> summaries) =>
        Availability(type, status, depth, answers, summaries, RequiredFor(depth));

    // The slots are a parameter so a test can exercise the chain while the catalog still lists no type for the new documents.
    internal static ArtifactAvailability Availability(
        ArtifactType type,
        InitiativeStatus status,
        InitiativeDepth? depth,
        ConversationAnswers? answers,
        IReadOnlyList<ArtifactSummary> summaries,
        IReadOnlyList<ArtifactSlot> required)
    {
        var coverage = Coverage(type, status, depth, answers, required);

        if (coverage != ArtifactAvailability.Available)
        {
            return coverage;
        }

        foreach (var upstream in Upstream(type))
        {
            if (!summaries.Any(s => s.Type == upstream && s.Status == ArtifactStatus.Approved))
            {
                return upstream == ArtifactType.Prd
                    ? ArtifactAvailability.WaitingForPrd
                    : ArtifactAvailability.WaitingForArchitecture;
            }
        }

        return ArtifactAvailability.Available;
    }

    /// <summary>
    /// The first upstream document edited, regenerated or approved again after the dependent was drafted, or null.
    /// It only advises: nothing is blocked and nothing is regenerated.
    /// </summary>
    public static ArtifactType? StaleUpstream(ArtifactType type, IReadOnlyList<ArtifactSummary> summaries)
    {
        var dependent = summaries.FirstOrDefault(s => s.Type == type);

        if (dependent is null)
        {
            return null;
        }

        foreach (var upstream in Upstream(type))
        {
            if (summaries.FirstOrDefault(s => s.Type == upstream) is { } source && source.UpdatedAt > dependent.UpdatedAt)
            {
                return upstream;
            }
        }

        return null;
    }

    private static ArtifactAvailability Coverage(
        ArtifactType type,
        InitiativeStatus status,
        InitiativeDepth? depth,
        ConversationAnswers? answers,
        IReadOnlyList<ArtifactSlot> required)
    {
        if (status == InitiativeStatus.Draft)
        {
            return ArtifactAvailability.InitiativeDraft;
        }

        if (depth is null)
        {
            return ArtifactAvailability.LevelNotSet;
        }

        if (!required.Any(slot => slot.Type == type))
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

    /// <summary>
    /// True when every document of the level has its coverage now. It ignores upstream approval on purpose: approving
    /// the PRD again while its dependents are approved must not count as a loss of coverage.
    /// </summary>
    public static bool IsSetAvailable(InitiativeStatus status, InitiativeDepth? depth, ConversationAnswers? answers)
    {
        var required = RequiredFor(depth);

        return required.All(slot => slot.Type is { } type
            && Coverage(type, status, depth, answers, required) == ArtifactAvailability.Available);
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
