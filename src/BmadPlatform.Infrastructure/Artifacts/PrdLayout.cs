using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// The PRD: sections 0 to 9 of BMAD in order. Each answer is quoted once; later sections that draw on the same
/// answer point to the identifiers it produced (FR, UJ) or to the section that holds it.
/// </summary>
internal static class PrdLayout
{
    public static void Write(DraftComposer c)
    {
        var large = c.Request.Depth == InitiativeDepth.Large;

        c.Title("PRD: " + c.InitiativeName);

        c.Heading("Propósito del documento");
        c.Line($"Este documento describe lo que se debe construir en la iniciativa «{c.InitiativeName}» para que el equipo de desarrollo pueda planificarlo y construirlo.");

        if (c.Description is { } description)
        {
            c.Blank();
            c.Line("Contexto: " + description);
        }

        c.Heading("Visión");
        c.Items(AssistantScript.Keys.Idea);

        c.Heading("Usuario objetivo");
        c.Items(AssistantScript.Keys.Users);

        c.Heading("Trabajos por hacer");
        c.Items(AssistantScript.Keys.Problem);

        c.Heading("No usuarios");
        c.Line("- " + c.Assumption("No se indicó qué personas no usarán la solución; se confirma con quien propone la iniciativa."));

        c.Heading("Recorridos clave");
        WriteJourneys(c);

        c.Heading("Glosario");
        c.Line("- " + c.Assumption("No se definieron términos propios de la iniciativa; se completa el glosario al revisar el documento con el equipo."));

        c.Heading("Funcionalidades");
        c.Raw("### Capacidades principales");
        c.Line("Lo que una persona debe poder hacer, en palabras de quien propuso la iniciativa.");
        c.Blank();
        c.Items(AssistantScript.Keys.Capabilities, c.Numbered(ItemPrefix.FunctionalRequirement));

        c.Raw("### Requisitos no funcionales");
        c.Line("Límites, reglas y condiciones de calidad que debe cumplir la solución.");
        c.Blank();
        WriteNonFunctional(c, large);

        if (large)
        {
            c.Raw("### Integraciones");
            c.Line("Sistemas y equipos con los que debe coordinarse la solución.");
            c.Blank();
            // A bare system name would read as a requirement title in BMAD's epics step, so each item is a sentence.
            c.Items(AssistantScript.Keys.Integrations, line => ArtifactOutline.Item(c.Next(ItemPrefix.FunctionalRequirement), "Debe integrarse con " + line));
        }

        c.Heading("No objetivos");
        c.Items(AssistantScript.Keys.OutOfScope);

        // The section needs at least one item, and an unknown answer gave only a reference to its question.
        if (c.Answer(AssistantScript.Keys.OutOfScope) is { IsUnknown: true })
        {
            c.Line("- " + c.Assumption("Mientras se define, se considera fuera del MVP todo lo que no esté en las funcionalidades."));
        }

        c.Heading("Alcance del MVP");
        c.Heading("Dentro");
        WriteInside(c);
        c.Heading("Fuera");
        c.Line("- Lo indicado en No objetivos (sección 5).");

        c.Heading("Métricas de éxito");
        c.Items(AssistantScript.Keys.Success, c.Numbered(ItemPrefix.SuccessMetric));

        c.OpenQuestions("Preguntas abiertas", "No hay preguntas abiertas.");
        c.AssumptionIndex();
    }

    // One journey per line of the users answer. The script never asks for a real journey, so the whole section carries
    // one shared assumption to confirm (decision D1), not one per journey.
    private static void WriteJourneys(DraftComposer c)
    {
        var users = c.Answer(AssistantScript.Keys.Users);

        if (users is { IsUnknown: false })
        {
            // The answer itself is quoted once, in section 2; each journey points to its line by position.
            var count = c.Lines(users.Text).Count;

            for (var position = 1; position <= count; position++)
            {
                var id = c.Next(ItemPrefix.UserJourney);

                c.Line(ArtifactOutline.Item(id, $"Recorrido de la persona o grupo n.º {position} del usuario objetivo (sección 2) para cumplir los trabajos por hacer."));
            }

            c.Line("- " + c.Assumption("Los recorridos de esta sección son un punto de partida; se confirman con las personas usuarias."));

            return;
        }

        var pending = users is null ? string.Empty : $" Ver {c.Question(AssistantScript.Keys.Users)}.";

        c.Line("- " + c.Assumption("Los recorridos clave se describirán cuando se conozca quién usará la solución.") + pending);
    }

    // Constraints and, for Large, qualities. A source without any answer is skipped while another one has one.
    private static void WriteNonFunctional(DraftComposer c, bool large)
    {
        string[] sources = large
            ? [AssistantScript.Keys.Constraints, AssistantScript.Keys.Qualities]
            : [AssistantScript.Keys.Constraints];

        var answered = sources.Where(key => c.Answer(key) is not null).ToList();

        if (answered.Count == 0)
        {
            c.Line("- " + c.Assumption("No se recibieron límites ni condiciones de calidad; se confirman con el equipo de desarrollo."));

            return;
        }

        foreach (var key in answered)
        {
            c.Items(key, c.Numbered(ItemPrefix.NonFunctionalRequirement));
        }
    }

    private static void WriteInside(DraftComposer c)
    {
        var requirements = c.Count(ItemPrefix.FunctionalRequirement);
        var capabilities = c.Answer(AssistantScript.Keys.Capabilities);

        if (requirements > 0)
        {
            c.Line("- Todas las funcionalidades de la sección 4.");
        }
        else if (capabilities is { IsUnknown: true })
        {
            c.Line($"- Pendiente: ver {c.Question(AssistantScript.Keys.Capabilities)}.");
        }
        else
        {
            c.Line("- " + c.Assumption("Lo que entra en el MVP se definirá cuando se conozcan las funcionalidades."));
        }

        // The priorities annotate what is inside; without an answer there is nothing to annotate.
        if (c.Answer(AssistantScript.Keys.Priorities) is not null)
        {
            c.Items(AssistantScript.Keys.Priorities, label: "Prioridad");
        }
    }
}
