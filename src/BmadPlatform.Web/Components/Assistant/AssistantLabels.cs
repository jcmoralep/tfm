using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Web.Components.Assistant;

public enum StepState
{
    Done,
    Current,
    Upcoming,
}

/// <summary>One row of the journey panel.</summary>
/// <param name="Detail">Progress or a short note under the step name; null when there is nothing to add.</param>
public sealed record JourneyStepInfo(JourneyStep Step, string Name, StepState State, string? Detail);

/// <summary>What the Conversación card on the initiative detail shows and offers.</summary>
/// <param name="ActionLabel">The link text; null when the card offers no action.</param>
public sealed record ConversationCardContent(string Text, string? ActionLabel);

/// <summary>Spanish texts of the chat page, kept as pure functions so they can be tested without rendering.</summary>
public static class AssistantLabels
{
    public const string PlanUndefinedText = "Se define al elegir el nivel";

    public const string Typing = "El asistente está escribiendo…";

    public const string UndoDone = "Se deshizo su última respuesta.";

    public const string UndoButton = "Deshacer mi última respuesta";

    public const string RetryButton = "Reintentar";

    public const string QuickRepliesLabel = "Respuestas rápidas";

    public const string LogLabel = "Conversación con el asistente";

    public const string ComposerLabel = "Su respuesta";

    public const string SendButton = "Enviar";

    public const string SendingCaption = "Enviando…";

    public const string DemoNotice =
        "Modo de demostración: escriba solo datos de ejemplo, no información real de la empresa ni datos personales.";

    public const string DraftCardText = "Termine de crear la iniciativa para conversar con el asistente.";

    public const string NotStartedCardText = "Hable con el asistente para aclarar su iniciativa paso a paso.";

    public const string NoConversationCardText = "No hay una conversación con el asistente para esta iniciativa.";

    public const string OpenAction = "Abrir asistente";

    public const string ContinueAction = "Continuar conversación";

    public const string ViewAction = "Ver conversación";

    /// <summary>
    /// Notes shown when unsent answers were given back to the composer: the buttons that were not sent (they
    /// cannot go into the box) and a text that grew past the limit. Empty when nothing needs saying.
    /// </summary>
    public static IReadOnlyList<string> RestoreNotes(DraftRestoration restoration)
    {
        List<string> notes = [];

        if (restoration.DroppedChoices.Count > 0)
        {
            var labels = string.Join(", ", restoration.DroppedChoices.Select(label => $"«{label}»"));

            notes.Add(restoration.DroppedChoices.Count == 1
                ? $"Su elección {labels} no se envió; vuelva a elegirla."
                : $"Sus elecciones {labels} no se enviaron; vuelva a elegirlas.");
        }

        if (restoration.TooLong)
        {
            notes.Add(AssistantTexts.AnswerTooLong);
        }

        return notes;
    }

    public static string StepName(JourneyStep step) => step switch
    {
        JourneyStep.Clarify => "Aclarar",
        JourneyStep.Plan => "Planificar",
        JourneyStep.ReadyToBuild => "Lista para construir",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
    };

    /// <summary>The prefix a screen reader hears before each message, since the bubble color carries no text.</summary>
    public static string RolePrefix(MessageRole role) => role switch
    {
        MessageRole.Assistant => "Asistente:",
        MessageRole.User => "Usted:",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    /// <summary>For example "3 de 5 preguntas"; "1 pregunta" in the singular.</summary>
    public static string QuestionProgress(PhaseProgress progress) =>
        $"{progress.Covered} de {progress.Total} {(progress.Total == 1 ? "pregunta" : "preguntas")}";

    public static string Remaining(PhaseProgress progress) => progress.Remaining switch
    {
        0 => "Completo",
        1 => "Falta 1",
        var count => $"Faltan {count}",
    };

    /// <summary>For example "3 de 5 preguntas · Faltan 2".</summary>
    public static string PhaseSummary(PhaseProgress progress) => $"{QuestionProgress(progress)} · {Remaining(progress)}";

    /// <summary>
    /// The overall "2 de 8 temas cubiertos" once the level is known; before that only the Aclarar questions
    /// are counted, because the total is not known yet.
    /// </summary>
    public static string OverallProgress(JourneySnapshot journey) =>
        journey.ProgressText ?? QuestionProgress(journey.Clarify);

    /// <summary>Share of the required topics covered, 0 to 100, for the progress bar.</summary>
    public static int OverallPercent(JourneySnapshot journey)
    {
        var progress = journey.Overall ?? journey.Clarify;

        return progress.Total == 0 ? 0 : Math.Clamp(progress.Covered * 100 / progress.Total, 0, 100);
    }

    /// <summary>
    /// The panel steps in order. Planificar is left out when the level has none (Small) and shows
    /// "Se define al elegir el nivel" while the level is not set. A step before the current one is done.
    /// </summary>
    public static IReadOnlyList<JourneyStepInfo> Steps(JourneySnapshot journey)
    {
        List<(JourneyStep Step, string? Detail)> included = [(JourneyStep.Clarify, PhaseSummary(journey.Clarify))];

        if (journey.PlanUndefined)
        {
            included.Add((JourneyStep.Plan, PlanUndefinedText));
        }
        else if (journey.Plan is { } plan)
        {
            included.Add((JourneyStep.Plan, PhaseSummary(plan)));
        }

        included.Add((JourneyStep.ReadyToBuild, null));

        // Small has no Planificar step: once it is planning, "lista para redactar" is the step it is on.
        var current = included.FindIndex(item => item.Step == journey.Step);
        if (current < 0)
        {
            current = included.Count - 1;
        }

        return
        [
            .. included.Select((item, index) => new JourneyStepInfo(
                item.Step,
                StepName(item.Step),
                index < current ? StepState.Done : index == current ? StepState.Current : StepState.Upcoming,
                item.Detail)),
        ];
    }

    /// <summary>The detail-page card for this initiative and conversation (spec: Detail page card).</summary>
    public static ConversationCardContent Card(ConversationView view) => view.Status switch
    {
        InitiativeStatus.Draft => new(DraftCardText, null),
        InitiativeStatus.ReadyToBuild => view.Started
            ? new(AssistantTexts.ReadyToBuildReadOnly, ViewAction)
            : new(NoConversationCardText, null),
        _ => view.Started
            ? new(OverallProgress(view.Journey), ContinueAction)
            : new(NotStartedCardText, OpenAction),
    };
}
