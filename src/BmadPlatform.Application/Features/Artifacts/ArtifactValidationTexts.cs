namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>Messages of the request validators. They only guard tampered requests; the pages never send these.</summary>
internal static class ArtifactValidationTexts
{
    public const string InitiativeRequired = "La iniciativa es obligatoria.";

    public const string TypeInvalid = "El tipo de documento no es válido.";

    public const string VersionInvalid = "La versión del documento no es válida.";
}
