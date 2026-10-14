using BmadPlatform.Application.Common.Exceptions;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// User-facing texts of the document use cases. The content rules (empty, too long) live in the domain and are
/// not repeated here.
/// </summary>
public static class ArtifactTexts
{
    public const string Conflict = "El documento cambió en otra pestaña. Si estaba editando, su texto sigue en el cuadro: cópielo, cancele y edite de nuevo la versión actual.";

    public const string AlreadyExists = "El documento ya existe. Use Regenerar para reemplazarlo.";

    public const string NotGenerated = "Todavía no se ha generado este documento.";

    public const string NotForLevel = "Este documento no corresponde al nivel de la iniciativa.";

    public const string ConfirmReplace = "Confirme que desea reemplazar el documento.";

    public const string DraftInitiative = "Termine de crear la iniciativa para generar documentos.";

    public const string TopicsUncovered = "Aún no se puede generar este documento: termine primero las preguntas del asistente.";

    public const string ComingLater = "Disponible en un paso posterior.";

    public const string WaitingForPrd = "Primero apruebe el PRD para generar la arquitectura.";

    public const string WaitingForArchitecture = "Primero apruebe la arquitectura para generar las épicas e historias.";

    // Why a model-backed draft failed. They are about the document and never say a message was saved: a failed draft stores nothing.
    public const string DraftQuota = "El asistente alcanzó su límite de uso por ahora. No se generó el documento; inténtelo de nuevo más tarde.";

    public const string DraftTimeout = "El asistente tardó demasiado en responder. No se generó el documento; inténtelo de nuevo.";

    public const string DraftMisconfigured = "El asistente no está configurado correctamente. No se generó el documento; avise a quien mantiene la plataforma.";

    public const string DraftBlocked = "El asistente no pudo redactar el documento con las respuestas actuales. Revise sus respuestas (puede deshacer la última y escribirla de otra forma) e inténtelo de nuevo.";

    public const string DraftMalformed = "El asistente devolvió un borrador que no se pudo usar. Inténtelo de nuevo.";

    public static string DraftUnavailableFor(AssistantUnavailableReason reason) => reason switch
    {
        AssistantUnavailableReason.Quota => DraftQuota,
        AssistantUnavailableReason.Auth or AssistantUnavailableReason.Model => DraftMisconfigured,
        AssistantUnavailableReason.Blocked => DraftBlocked,
        AssistantUnavailableReason.Malformed => DraftMalformed,
        _ => DraftTimeout,
    };

    public static string UnassignedQuestions(IEnumerable<string> ids) =>
        $"Asigne un responsable y una condición de revisión a las preguntas pendientes antes de aprobar: {string.Join(", ", ids)}.";

    public static string QuestionNotFound(string id) => $"La pregunta {id} no existe en este documento.";

    /// <summary>The plain reason a document cannot be generated now; null when it can.</summary>
    public static string? ReasonFor(ArtifactAvailability availability) => availability switch
    {
        ArtifactAvailability.Available => null,
        ArtifactAvailability.InitiativeDraft => DraftInitiative,
        ArtifactAvailability.LevelNotSet or ArtifactAvailability.NotRequired => NotForLevel,
        ArtifactAvailability.NotYetSupported => ComingLater,
        ArtifactAvailability.WaitingForPrd => WaitingForPrd,
        ArtifactAvailability.WaitingForArchitecture => WaitingForArchitecture,
        _ => TopicsUncovered,
    };
}
