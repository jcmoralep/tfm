using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Assistant.Script;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>The Brief: the BMAD sections in order, fed by the five Aclarar topics.</summary>
internal static class BriefLayout
{
    public static void Write(DraftComposer c)
    {
        c.Title("Brief: " + c.InitiativeName);

        c.Heading("Resumen ejecutivo");
        c.Line(c.Description is { } description
            ? $"Iniciativa «{c.InitiativeName}»: {description}"
            : $"Iniciativa «{c.InitiativeName}».");

        c.Heading("El problema");
        c.Items(AssistantScript.Keys.Problem);

        c.Heading("La solución");
        c.Items(AssistantScript.Keys.Idea);

        c.Heading("Qué lo hace distinto");
        c.Line("- " + c.Assumption("Aún no se ha descrito qué diferencia a esta solución de otras opciones; se confirma con quien propone la iniciativa."));

        c.Heading("A quién sirve");
        c.Items(AssistantScript.Keys.Users);

        c.Heading("Criterios de éxito");
        c.Items(AssistantScript.Keys.Success, c.Numbered(ItemPrefix.SuccessMetric));

        c.Heading("Alcance");
        c.Raw("### Dentro");
        c.Line("- " + c.Assumption("Lo que entra en el alcance se define con el equipo durante la planificación."));
        c.Raw("### Fuera");
        c.Items(AssistantScript.Keys.OutOfScope);

        c.Heading("Visión");
        c.Line("- " + c.Assumption("Aún no se ha descrito la visión a largo plazo; se confirma con quien propone la iniciativa."));

        c.AssumptionIndex();
        c.OpenQuestions("Preguntas pendientes", "No hay preguntas pendientes.");
    }
}
