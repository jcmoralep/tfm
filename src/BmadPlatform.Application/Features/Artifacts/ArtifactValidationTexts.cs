namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>Messages of the request validators. They only guard tampered requests; the pages never send these.</summary>
internal static class ArtifactValidationTexts
{
    public const string InitiativeRequired = "La iniciativa es obligatoria.";

    public const string TypeInvalid = "El tipo de documento no es válido.";

    public const string VersionInvalid = "La versión del documento no es válida.";

    public const string NoQuestions = "No hay preguntas para guardar.";

    public const string DuplicateQuestions = "Hay preguntas repetidas en el envío.";

    public const string TooManyQuestions = "Hay demasiadas preguntas para guardar a la vez.";

    public const string QuestionIdInvalid = "Alguna pregunta no tiene un identificador válido.";

    public const string OwnerTooLong = "El responsable no puede superar los 60 caracteres.";

    public const string RevisitTooLong = "La condición de revisión no puede superar los 80 caracteres.";

    public const string OwnerForbidden = "El responsable no puede contener paréntesis, corchetes, comillas invertidas, ni los signos ; * | \\ < >, ni caracteres de control o saltos de línea.";

    public const string RevisitForbidden = "La condición de revisión no puede contener paréntesis, corchetes, comillas invertidas, ni los signos ; * | \\ < >, ni caracteres de control o saltos de línea.";
}
