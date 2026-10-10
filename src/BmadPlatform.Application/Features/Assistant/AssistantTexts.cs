using BmadPlatform.Application.Common.Exceptions;

namespace BmadPlatform.Application.Features.Assistant;

/// <summary>
/// User-facing texts of the assistant use cases. Rejections that the domain already words (empty answer, undo
/// refusals, pending reply) live in the domain and are not repeated here.
/// </summary>
public static class AssistantTexts
{
    public const string DraftNotAllowed = "Termine de crear la iniciativa para abrir el asistente.";

    public const string ReadyToBuildReadOnly = "La iniciativa ya está lista para construir; la conversación es solo de lectura.";

    public const string Conflict = ConflictException.DefaultMessage;

    public const string Unavailable = AssistantUnavailableException.DefaultMessage;

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
