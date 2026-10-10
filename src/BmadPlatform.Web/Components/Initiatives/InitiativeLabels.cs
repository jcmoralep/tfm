using System.Globalization;
using BmadPlatform.Application.Features.Initiatives;
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

    public static string Depth(InitiativeDepth depth) => InitiativeTexts.LevelName(depth);

    /// <summary>
    /// What a list row or the detail shows for the depth: the level, "Pendiente de sugerencia" while the
    /// assistant has not suggested one, or "Sin elegir" for a draft that has not reached that choice.
    /// </summary>
    public static string DepthSummary(DepthMode? mode, InitiativeDepth? depth) =>
        depth is { } level ? Depth(level)
        : mode == DepthMode.Automatic ? InitiativeTexts.DepthPending
        : InitiativeTexts.LevelUnset;

    /// <summary>The mode name, or "Sin elegir" while the user has not chosen one.</summary>
    public static string ModeSummary(DepthMode? mode) => mode is { } value ? Mode(value) : InitiativeTexts.ModeUnset;

    /// <summary>Day, month, year and time, for example "08/10/2026 14:30". The server's local time is used.</summary>
    public static string Date(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The documents each depth level produces, in the order they are delivered.</summary>
    public static IReadOnlyList<string> Deliverables(InitiativeDepth depth) => InitiativeTexts.Deliverables(depth);

    /// <summary>The deliverables as one sentence fragment, for example "PRD, Arquitectura y Épicas e historias".</summary>
    public static string DeliverablesSummary(InitiativeDepth depth) => InitiativeTexts.DeliverablesSummary(depth);
}
