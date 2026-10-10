using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Assistant.Script;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>The five-part Specification of BMAD plus its assumptions and open questions, for the Small level.</summary>
internal static class SpecLayout
{
    public static void Write(DraftComposer c)
    {
        c.Title("Especificación: " + c.InitiativeName);

        c.Heading("Por qué");
        c.Items(AssistantScript.Keys.Idea, label: "La idea");
        c.JoinedItem(AssistantScript.Keys.Users, "Quién lo usará");
        c.Items(AssistantScript.Keys.Problem, label: "El problema");

        c.Heading("Capacidades");
        c.Items(AssistantScript.Keys.Capabilities, c.Numbered(ItemPrefix.FunctionalRequirement));

        c.Heading("Restricciones");
        c.Line("- " + c.Assumption("En el nivel Pequeña no se preguntó por restricciones; se confirman con el equipo de desarrollo antes de construir."));

        c.Heading("No objetivos");
        c.Items(AssistantScript.Keys.OutOfScope);

        // The section needs at least one item, and an unknown answer gave only a reference to its question.
        if (c.Answer(AssistantScript.Keys.OutOfScope) is { IsUnknown: true })
        {
            c.Line("- " + c.Assumption("Mientras se define, se considera fuera de alcance todo lo que no esté en las capacidades."));
        }

        c.Heading("Señal de éxito");
        c.Items(AssistantScript.Keys.Success, c.Numbered(ItemPrefix.SuccessMetric));

        c.AssumptionIndex();
        c.OpenQuestions("Preguntas abiertas", "No hay preguntas abiertas.");
    }
}
