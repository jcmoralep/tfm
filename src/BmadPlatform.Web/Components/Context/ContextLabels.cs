using System.Globalization;
using BmadPlatform.Application.Features.Context;
using BmadPlatform.Domain.Context;

namespace BmadPlatform.Web.Components.Context;

/// <summary>Spanish texts and size formatting of the Contexto card, pure so they can be tested.</summary>
public static class ContextLabels
{
    public const string CardTitle = "Contexto";

    public const string CardIntro = "Adjunte archivos que ayuden a entender la idea. Son opcionales.";

    public const string EmptyText = "Todavía no hay elementos en el contexto.";

    public const string UnavailableText = "No se pudo leer el contexto en este momento.";

    public const string UploadLabel = "Añadir archivos";

    public const string UploadHint = "Hasta 5 archivos a la vez. No se permiten programas ni scripts.";

    public const string Added = "añadido";

    public const string Uploading = "Subiendo archivos…";

    public const string DownloadAction = "Descargar";

    public const string TooManyFilesSelected = "Seleccione como máximo 5 archivos a la vez.";

    public const string UploadFailed = "No se pudo añadir el archivo. Inténtelo de nuevo.";

    /// <summary>Files per selection the card accepts. A UI guard only, not a product limit.</summary>
    public const int MaxFilesPerSelection = 5;

    private static readonly NumberFormatInfo Spanish = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    /// <summary>"2,4 MB" from one megabyte up; whole kilobytes (rounded up, so "0 KB" only for an empty file) below.</summary>
    public static string SizeLabel(long bytes)
    {
        const long Kilobyte = 1024;
        const long Megabyte = 1024 * 1024;

        if (bytes < Megabyte)
        {
            var kilobytes = (bytes + Kilobyte - 1) / Kilobyte;

            return $"{Math.Max(kilobytes, 0)} KB";
        }

        return $"{(bytes / (double)Megabyte).ToString("0.#", Spanish)} MB";
    }

    public static string Counter(int count) => $"{count} de {ContextLimits.MaxItems} elementos";

    public static string UsedSize(long totalBytes) =>
        $"{SizeLabel(totalBytes)} de {SizeLabel(ContextLimits.MaxTotalBytes)} usados";

    public static string CapReached(int count) =>
        count >= ContextLimits.MaxItems
            ? $"Se alcanzó el máximo de {ContextLimits.MaxItems} elementos. No se pueden añadir más."
            : string.Empty;

    public static string KindLabel(AttachmentKind kind) => kind switch
    {
        AttachmentKind.File => "Archivo",
        AttachmentKind.Link => "Enlace",
        _ => "Nota",
    };

    /// <summary>The second line of a row: the size of a file, the kind of the rest.</summary>
    public static string RowDetail(AttachmentItem item) =>
        item.Kind == AttachmentKind.File && item.SizeBytes is { } size
            ? $"{KindLabel(item.Kind)} · {SizeLabel(size)}"
            : KindLabel(item.Kind);

    public static string ResultAdded(string fileName) => $"{fileName}: {Added}";
}
