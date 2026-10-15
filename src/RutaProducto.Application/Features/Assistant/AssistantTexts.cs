using RutaProducto.Application.Common.Exceptions;

namespace RutaProducto.Application.Features.Assistant;

/// <summary>
/// User-facing texts of the assistant use cases. Rejections that the domain already words (empty answer, undo
/// refusals, pending reply) live in the domain and are not repeated here.
/// </summary>
public static class AssistantTexts
{
    public const string DraftNotAllowed = "Termine de crear la iniciativa para abrir el asistente.";

    public const string ReadyToBuildReadOnly = "La iniciativa ya está lista para construir; la conversación es solo de lectura.";

    public const string Conflict = ConflictException.DefaultMessage;

    /// <summary>Timeout and the generic case. Chat texts say the message was saved because the answer is stored first.</summary>
    public const string Unavailable =
        "El asistente no está disponible en este momento. Su mensaje quedó guardado; inténtelo de nuevo en unos minutos.";

    public const string Quota =
        "El asistente alcanzó su límite de uso por ahora. Su mensaje quedó guardado; pulse Reintentar más tarde.";

    public const string Misconfigured =
        "El asistente no está configurado correctamente. Su mensaje quedó guardado; avise a quien mantiene la plataforma.";

    /// <summary>Appended to the script wording when the provider blocked the reply.</summary>
    public const string RephraseHint = "Si lo prefiere, puede deshacer su última respuesta y escribirla de otra forma.";

    /// <summary>
    /// The chat message for a failure reason. Blocked and Malformed are normally handled by a fallback to the script;
    /// if one reaches this point it gets the generic text.
    /// </summary>
    public static string UnavailableFor(AssistantUnavailableReason reason) => reason switch
    {
        AssistantUnavailableReason.Quota => Quota,
        AssistantUnavailableReason.Auth or AssistantUnavailableReason.Model => Misconfigured,
        _ => Unavailable,
    };

    public const string IncompleteConfirmation = "Aún faltan preguntas por responder antes de pasar a Planificar.";

    public const string EmptyAnswer = "La respuesta no puede estar vacía.";

    public const string AnswerTooLong = "La respuesta no puede superar los 2000 caracteres.";

    public const string AnswerOrOptionOnly = "Escriba una respuesta o elija una opción.";

    public const string InvalidQuickReply = "La respuesta rápida no es válida para esta pregunta.";

    public const string ConversationNotStarted = "Abra la conversación con el asistente antes de enviar una respuesta.";

    public const string NothingLeftToAnswer = "La etapa de preguntas ya terminó; no hay nada más que responder.";

    /// <summary>Spoken by the assistant when the user chose "Elegir otro nivel".</summary>
    public const string ChooseLevelPrompt = "Elija el nivel de documentación que prefiere.";
}
