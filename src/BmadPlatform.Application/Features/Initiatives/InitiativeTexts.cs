using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Initiatives;

/// <summary>User-facing texts shared by the initiatives use cases and pages.</summary>
public static class InitiativeTexts
{
    public const string DepthPending = "Pendiente de sugerencia";

    /// <summary>Shown for a depth mode that has not been chosen yet (a draft that has not reached that step).</summary>
    public const string ModeUnset = "Sin elegir";

    /// <summary>Shown for a depth level that has not been chosen yet.</summary>
    public const string LevelUnset = "Sin elegir";

    public const string NotFound = "La iniciativa no existe.";

    public static string LevelName(InitiativeDepth depth) => depth switch
    {
        InitiativeDepth.Small => "Pequeña",
        InitiativeDepth.Standard => "Estándar",
        InitiativeDepth.Large => "Grande",
        _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, null),
    };

    /// <summary>The documents each depth level produces, in the order they are delivered (RF-27).</summary>
    public static IReadOnlyList<string> Deliverables(InitiativeDepth depth) => depth switch
    {
        InitiativeDepth.Small => ["Una especificación breve"],
        InitiativeDepth.Standard => ["Brief", "PRD"],
        InitiativeDepth.Large => ["PRD", "Arquitectura", "Épicas e historias"],
        _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, null),
    };

    /// <summary>The deliverables as one sentence fragment, for example "PRD, Arquitectura y Épicas e historias".</summary>
    public static string DeliverablesSummary(InitiativeDepth depth)
    {
        var items = Deliverables(depth);

        return items.Count == 1
            ? items[0]
            : string.Join(", ", items.Take(items.Count - 1)) + " y " + items[^1];
    }

    /// <summary>
    /// The summary for the middle of a sentence: a single item that starts with an article ("Una especificación
    /// breve") loses its capital, while document names such as "Brief" or "PRD" keep theirs.
    /// </summary>
    public static string DeliverablesInSentence(InitiativeDepth depth)
    {
        var summary = DeliverablesSummary(depth);

        return summary.StartsWith("Una ", StringComparison.Ordinal) ? "u" + summary[1..] : summary;
    }
}
