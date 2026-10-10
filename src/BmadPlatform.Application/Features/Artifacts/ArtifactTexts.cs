namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// User-facing texts of the document use cases. The content rules (empty, too long) live in the domain and are
/// not repeated here.
/// </summary>
public static class ArtifactTexts
{
    public const string Conflict = "El documento cambió en otra pestaña. Recargue la página.";

    public const string AlreadyExists = "El documento ya existe. Use Regenerar para reemplazarlo.";

    public const string NotGenerated = "Todavía no se ha generado este documento.";

    public const string NotForLevel = "Este documento no corresponde al nivel de la iniciativa.";

    public const string ConfirmReplace = "Confirme que desea reemplazar el documento.";

    public const string DraftInitiative = "Termine de crear la iniciativa para generar documentos.";

    public const string TopicsUncovered = "Aún no se puede generar este documento: termine primero las preguntas del asistente.";
}
