using RutaProducto.Application.Common.Exceptions;

namespace RutaProducto.Infrastructure.Gemini;

/// <summary>
/// Neutral Spanish messages for the exception raised by the client. They never claim that anything was saved
/// (a document draft stores nothing); the callers add their own wording where it applies.
/// </summary>
internal static class GeminiTexts
{
    public static string For(AssistantUnavailableReason reason) => reason switch
    {
        AssistantUnavailableReason.Quota =>
            "Se alcanzó el límite de uso del asistente por ahora. Inténtelo de nuevo más tarde.",
        AssistantUnavailableReason.Auth =>
            "El asistente no tiene acceso autorizado (credenciales). Avise a la persona que administra la plataforma.",
        AssistantUnavailableReason.Model =>
            "El asistente no está configurado correctamente (modelo o solicitud). Avise a la persona que administra la plataforma.",
        AssistantUnavailableReason.Blocked =>
            "El asistente no pudo responder a este contenido. Reformúlelo e inténtelo de nuevo.",
        AssistantUnavailableReason.Malformed =>
            "El asistente devolvió una respuesta que no se pudo usar. Inténtelo de nuevo.",
        _ => "El asistente tardó demasiado en responder. Inténtelo de nuevo en unos minutos.",
    };
}
