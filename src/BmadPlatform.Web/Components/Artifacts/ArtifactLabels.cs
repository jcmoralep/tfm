using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Web.Components.Artifacts;

/// <summary>Visual weight of a state badge: amber while in progress, blue for something to do, green when done.</summary>
public enum ArtifactTone
{
    Neutral,
    Action,
    InProgress,
    Done,
}

/// <summary>One row of the Artefactos card. <paramref name="Action"/> is null when the row offers none.</summary>
public sealed record ArtifactRowContent(
    string Label,
    string State,
    ArtifactTone Tone,
    string? Reason,
    ArtifactAction? Action,
    ArtifactType? Type);

public enum ArtifactAction
{
    Generate,
    Open,
}

/// <summary>Spanish texts, route slugs and state decisions of the document screens, pure so they can be tested.</summary>
public static class ArtifactLabels
{
    public const string SpecSlug = "especificacion";

    public const string BriefSlug = "brief";

    public const string PrdSlug = "prd";

    /// <summary>Query string flag the card adds so the document page can show the green "generated" notice.</summary>
    public const string GeneratedFlag = "generado";

    public const string NotGenerated = "Sin generar";

    public const string Available = "Disponible";

    public const string NotAvailable = "Aún no disponible";

    public const string GenerateAction = "Generar";

    public const string OpenAction = "Abrir";

    public const string Generating = "Generando el documento…";

    /// <summary>Shown when an action fails for a reason that is not a business rule; the details go to the log.</summary>
    public const string ActionFailed = "No se pudo completar la acción. Intente de nuevo.";

    public const string CardTitle = "Artefactos";

    public const string CardIntro = "Documentos que su iniciativa necesita antes de pasar a desarrollo.";

    public const string NoDocumentsText = "Aún no hay documentos para este nivel.";

    public const string UnavailableCardText = "No se pudieron cargar los documentos de esta iniciativa.";

    public const string Generated = "Documento generado.";

    public const string Approved = "Documento aprobado.";

    public const string ApprovedReady = "Documento aprobado. La iniciativa está Lista para construir.";

    public const string Regenerating = "Regenerando el documento…";

    public const string Saving = "Guardando los cambios…";

    public const string Approving = "Aprobando el documento…";

    public const string Regenerated = "Documento regenerado.";

    public const string EditorLabel = "Contenido en Markdown";

    /// <summary>The limit as shown in the counter; the thousands separator is written literally to stay culture independent.</summary>
    public const string ContentLimitText = "20.000";

    public const string QuestionsTitle = "Preguntas pendientes";

    public const string QuestionsIntro = "Guardar cuenta como una edición del documento: se crea una versión nueva y queda en Borrador.";

    public const string QuestionsRequiredLegend = "necesario para aprobar";

    public const string QuestionsUnassigned = "Sin asignar";

    public const string SavingQuestions = "Guardando las preguntas pendientes…";

    public const string QuestionsUnchanged = "No había cambios que guardar.";

    public const string RegenerateOwnersLost = "Los responsables de las preguntas vuelven a «por asignar».";

    public const string OwnerPlaceholderHint = "antes de la arquitectura";

    /// <summary>Suggested areas for the owner input; any text is still allowed.</summary>
    public static readonly IReadOnlyList<string> OwnerSuggestions = ["Legal", "TI", "Finanzas", "Quien propone la iniciativa"];

    public static string Saved(int version) => $"Cambios guardados. Versión {version}.";

    public static string QuestionsSaved(int version) => $"Preguntas guardadas. Versión {version}.";

    /// <summary>The amber note under the header that says why Aprobar is disabled.</summary>
    public static string ApproveBlocked(IEnumerable<string> unassignedIds) =>
        $"{ArtifactTexts.UnassignedQuestions(unassignedIds)} Use el panel «{QuestionsTitle}».";

    public static string EditCounter(int length) => $"{length} / {ContentLimitText}";

    /// <summary>What saving an edit of an approved document will undo; null for a draft, where nothing changes state.</summary>
    public static string? EditHint(ArtifactStatus? status, InitiativeStatus initiativeStatus) => status switch
    {
        ArtifactStatus.Approved when initiativeStatus == InitiativeStatus.ReadyToBuild =>
            "Al guardar, el documento vuelve a Borrador y la iniciativa vuelve a Planificando.",
        ArtifactStatus.Approved => "Al guardar, el documento vuelve a Borrador.",
        _ => null,
    };

    public static string TypeName(ArtifactType type) => type switch
    {
        ArtifactType.Spec => "Especificación",
        ArtifactType.Brief => "Brief",
        ArtifactType.Prd => "PRD",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>One plain-language line under the document name, for people who do not know the term.</summary>
    public static string TypeHelp(ArtifactType type) => type switch
    {
        ArtifactType.Spec => "Descripción corta de lo que se va a construir.",
        ArtifactType.Brief => "Resumen de la idea: el problema, para quién es y qué cambia.",
        ArtifactType.Prd => "Qué debe hacer el producto y cómo se medirá el éxito.",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static string StatusName(ArtifactStatus status) => status switch
    {
        ArtifactStatus.Draft => "Borrador",
        ArtifactStatus.Approved => "Aprobado",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string Slug(ArtifactType type) => type switch
    {
        ArtifactType.Spec => SpecSlug,
        ArtifactType.Brief => BriefSlug,
        ArtifactType.Prd => PrdSlug,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>Exact, case-sensitive match: anything else is an unknown document.</summary>
    public static bool TryParseSlug(string? slug, out ArtifactType type)
    {
        switch (slug)
        {
            case SpecSlug:
                type = ArtifactType.Spec;
                return true;
            case BriefSlug:
                type = ArtifactType.Brief;
                return true;
            case PrdSlug:
                type = ArtifactType.Prd;
                return true;
            default:
                type = default;
                return false;
        }
    }

    public static string PagePath(Guid initiativeId, ArtifactType type) =>
        $"/iniciativas/{initiativeId}/artefactos/{Slug(type)}";

    /// <summary>"Borrador v2" or "Aprobado v3".</summary>
    public static string VersionedStatus(ArtifactStatus status, int version) => $"{StatusName(status)} v{version}";

    public static ArtifactTone StatusTone(ArtifactStatus status) =>
        status == ArtifactStatus.Approved ? ArtifactTone.Done : ArtifactTone.InProgress;

    /// <summary>What a row of the Artefactos card shows and offers for one slot.</summary>
    public static ArtifactRowContent Row(ArtifactSlotView slot)
    {
        if (slot.Generated && slot.Type is not null && slot.Status is { } status)
        {
            return new(slot.Label, VersionedStatus(status, slot.Version), StatusTone(status), null, ArtifactAction.Open, slot.Type);
        }

        if (slot.Type is not null && slot.Availability == ArtifactAvailability.Available)
        {
            return new(slot.Label, Available, ArtifactTone.Action, null, ArtifactAction.Generate, slot.Type);
        }

        return new(slot.Label, NotAvailable, ArtifactTone.Neutral, slot.Reason, null, slot.Type);
    }
}
