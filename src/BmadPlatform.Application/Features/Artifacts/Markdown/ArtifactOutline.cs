using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>
/// One required heading. <paramref name="Number"/> is empty for the unnumbered Brief and Spec sections, and
/// <paramref name="Level"/> is the Markdown heading level (2 for a top section, 3 for a subsection).
/// </summary>
public sealed record OutlineSection(int Level, string Number, string Title)
{
    public string Heading => new string('#', Level) + " " + (Number.Length == 0 ? Title : Number + " " + Title);
}

/// <summary>
/// The required Spanish headings of each document type, taken from the BMAD structures in
/// <c>docs/bmad-referencia.md</c> section 2, and the canonical lines for identifiers and assumptions.
/// </summary>
public static class ArtifactOutline
{
    public const string AssumptionIndexTitle = "Índice de suposiciones";
    public const string DevelopmentQuestionsTitle = "Preguntas para desarrollo";
    public const string CoverageMapTitle = "Mapa de cobertura de requisitos";

    private static readonly OutlineSection[] Brief =
    [
        Top("Resumen ejecutivo"),
        Top("El problema"),
        Top("La solución"),
        Top("Qué lo hace distinto"),
        Top("A quién sirve"),
        Top("Criterios de éxito"),
        Top("Alcance"),
        Top("Visión"),
        Top(AssumptionIndexTitle),
        Top("Preguntas pendientes"),
    ];

    private static readonly OutlineSection[] Prd =
    [
        Top("Propósito del documento", "0"),
        Top("Visión", "1"),
        Top("Usuario objetivo", "2"),
        Sub("Trabajos por hacer", "2.1"),
        Sub("No usuarios", "2.2"),
        Sub("Recorridos clave", "2.3"),
        Top("Glosario", "3"),
        Top("Funcionalidades", "4"),
        Top("No objetivos", "5"),
        Top("Alcance del MVP", "6"),
        Sub("Dentro", "6.1"),
        Sub("Fuera", "6.2"),
        Top("Métricas de éxito", "7"),
        Top("Preguntas abiertas", "8"),
        Top(AssumptionIndexTitle, "9"),
    ];

    private static readonly OutlineSection[] Spec =
    [
        Top("Por qué"),
        Top("Capacidades"),
        Top("Restricciones"),
        Top("No objetivos"),
        Top("Señal de éxito"),
        Top(AssumptionIndexTitle),
        Top("Preguntas abiertas"),
    ];

    public static IReadOnlyList<OutlineSection> Sections(ArtifactType type) => type switch
    {
        ArtifactType.Brief => Brief,
        ArtifactType.Prd => Prd,
        ArtifactType.Spec => Spec,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>The canonical item line, for example <c>- **FR-1**: texto</c>.</summary>
    public static string Item(string id, string text) => $"- **{id}**: {text}";

    /// <summary>The inline assumption marker, for example <c>[ASSUMPTION: A-1]</c>.</summary>
    public static string AssumptionMarker(string id) => $"[ASSUMPTION: {id}]";

    private static OutlineSection Top(string title, string number = "") => new(2, number, title);

    private static OutlineSection Sub(string title, string number) => new(3, number, title);
}
