using System.Globalization;
using RutaProducto.Application.Features.Initiatives.GetDashboard;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Web.Components.Home;

/// <summary>Spanish texts, links and bar sizes of the home dashboard. Pure, so they can be tested without a browser.</summary>
public static class DashboardLabels
{
    /// <summary>Bars are drawn in this many steps (0 to 10) because views cannot carry inline styles.</summary>
    public const int BarSteps = 10;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string StatusHint(InitiativeStatus status) => status switch
    {
        InitiativeStatus.Draft => "Ideas que aún está describiendo.",
        InitiativeStatus.Clarifying => "El asistente le hace preguntas para entenderlas mejor.",
        InitiativeStatus.Planning => "Se están preparando sus documentos.",
        InitiativeStatus.ReadyToBuild => "Listas para entregar a desarrollo.",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>The one next step offered for an initiative of that status.</summary>
    public static (string Label, string Href) NextStep(Guid id, InitiativeStatus status) => status switch
    {
        InitiativeStatus.Draft => ("Completar detalles", $"/iniciativas/{id}/crear"),
        InitiativeStatus.Clarifying => ("Continuar conversación", $"/iniciativas/{id}/asistente"),
        InitiativeStatus.Planning => ("Ver documentos", $"/iniciativas/{id}"),
        InitiativeStatus.ReadyToBuild => ("Ver y descargar", $"/iniciativas/{id}"),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>Where the name of an initiative leads: drafts resume the wizard, the rest open the detail.</summary>
    public static string OpenHref(Guid id, InitiativeStatus status) =>
        status == InitiativeStatus.Draft ? $"/iniciativas/{id}/crear" : $"/iniciativas/{id}";

    /// <summary>
    /// How long ago something happened: "hace un momento", "hace 5 minutos", "hace 2 horas", "ayer", "hace 3 días",
    /// and the date (dd/MM/yyyy, UTC) from a week on.
    /// </summary>
    public static string RelativeTime(DateTimeOffset value, DateTimeOffset now)
    {
        var elapsed = now - value;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "hace un momento";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return Plural((int)elapsed.TotalMinutes, "minuto");
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return Plural((int)elapsed.TotalHours, "hora");
        }

        if (elapsed < TimeSpan.FromDays(2))
        {
            return "ayer";
        }

        if (elapsed < TimeSpan.FromDays(7))
        {
            return $"hace {(int)elapsed.TotalDays} días";
        }

        return value.UtcDateTime.ToString("dd/MM/yyyy", Invariant);
    }

    /// <summary>The bar step for a value against the largest value shown: 0 only for 0, otherwise 1 to 10.</summary>
    public static int BarLevel(int value, int max)
    {
        if (value <= 0 || max <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(value * (double)BarSteps / max), 1, BarSteps);
    }

    public static string Percent(int percent) => $"{percent} %";

    /// <summary>For example "3,5 días" or "1 día"; "—" while no initiative has reached ready.</summary>
    public static string AverageDays(double? days) => days switch
    {
        null => "—",
        1 => "1 día",
        { } value => $"{value.ToString("0.#", Invariant).Replace('.', ',')} días",
    };

    public static string WeekLabel(DateOnly weekStart) => weekStart.ToString("dd/MM", Invariant);

    /// <summary>Text alternative of the weekly chart: every value, so nothing depends on seeing the bars.</summary>
    public static string ChartSummary(IReadOnlyList<DashboardWeek> weeks)
    {
        var parts = weeks.Select(week =>
            $"semana del {WeekLabel(week.WeekStart)}: {week.Created} creadas, {week.Ready} listas para construir");

        return $"Actividad de las últimas {weeks.Count} semanas. {string.Join("; ", parts)}.";
    }

    private static string Plural(int amount, string unit) => amount == 1 ? $"hace 1 {unit}" : $"hace {amount} {unit}s";
}
