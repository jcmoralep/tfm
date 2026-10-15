namespace BmadPlatform.Application.Features.Context;

/// <summary>User-facing texts of the context use cases.</summary>
public static class ContextTexts
{
    public const string NotFound = "El elemento no existe.";

    public const string TooManyItems = "El contexto ya tiene el máximo de 20 elementos.";

    public const string TotalTooLarge = "Los archivos del contexto no pueden superar los 50 MB en total.";

    /// <summary>The per-file message names the configured cap, in whole megabytes.</summary>
    public static string FileTooLarge(long maxFileBytes) =>
        $"El archivo supera el máximo de {maxFileBytes / (1024 * 1024)} MB.";
}
