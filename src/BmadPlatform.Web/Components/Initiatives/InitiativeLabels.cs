using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Web.Components.Initiatives;

/// <summary>Spanish display names for the initiative enums and what each depth level produces (RF-27).</summary>
public static class InitiativeLabels
{
    public static string Status(InitiativeStatus status) => status switch
    {
        InitiativeStatus.Draft => "Borrador",
        InitiativeStatus.Clarifying => "Aclarando",
        InitiativeStatus.Planning => "Planificando",
        InitiativeStatus.ReadyToBuild => "Lista para construir",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string Mode(DepthMode mode) => mode switch
    {
        DepthMode.Manual => "Manual",
        DepthMode.Automatic => "Automático",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static string ModeDescription(DepthMode mode) => mode switch
    {
        DepthMode.Manual => "Usted elige el nivel de profundidad.",
        DepthMode.Automatic => "El asistente sugerirá el nivel más adelante.",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static string Depth(InitiativeDepth depth) => depth switch
    {
        InitiativeDepth.Small => "Pequeña",
        InitiativeDepth.Standard => "Estándar",
        InitiativeDepth.Large => "Grande",
        _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, null),
    };

    /// <summary>The documents each depth level produces, in the order they are delivered.</summary>
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
}
