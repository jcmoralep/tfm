using RutaProducto.Domain.Common;

namespace RutaProducto.Domain.Context;

/// <summary>
/// One item of context of an initiative: a file (bytes live in file storage, only metadata and the key here), a
/// link or a note. Attachments are added and deleted, never edited. Factories validate every rule so the domain
/// cannot be bypassed; validators reuse the constants and predicates below.
/// </summary>
public sealed class Attachment
{
    public const int UrlMaxLength = 2_000;

    public const int NoteTitleMaxLength = 120;

    public const int NoteTextMaxLength = 5_000;

    public const int ContentTypeMaxLength = 255;

    public const int StorageKeyMaxLength = 65;

    public const string DefaultContentType = "application/octet-stream";

    public const string EmptyFileMessage = "El archivo está vacío.";

    public const string BlockedFileMessage = "Este tipo de archivo no se puede adjuntar por seguridad.";

    public const string InvalidUrlMessage = "Escriba un enlace completo que empiece por http:// o https://.";

    public const string UrlTooLongMessage = "El enlace no puede superar los 2000 caracteres.";

    public const string EmptyNoteTitleMessage = "El título de la nota es obligatorio.";

    public const string NoteTitleTooLongMessage = "El título de la nota no puede superar los 120 caracteres.";

    public const string EmptyNoteTextMessage = "El texto de la nota es obligatorio.";

    public const string NoteTextTooLongMessage = "El texto de la nota no puede superar los 5000 caracteres.";

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".scr", ".js", ".vbs", ".ps1", ".sh", ".jar",
    };

    // Required by EF Core to materialize the entity.
    private Attachment()
    {
        OwnerId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid InitiativeId { get; private set; }

    public string OwnerId { get; private set; }

    public AttachmentKind Kind { get; private set; }

    /// <summary>The normalized file name of a file or the title of a note; null for a link.</summary>
    public string? Name { get; private set; }

    public string? Url { get; private set; }

    public string? Text { get; private set; }

    /// <summary>Kept for information only; it never decides acceptance or the download headers.</summary>
    public string? ContentType { get; private set; }

    public long? SizeBytes { get; private set; }

    public string? StorageKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Whether the server read the text of a file; <see cref="ExtractionStatus.NotApplicable"/> for everything else.</summary>
    public ExtractionStatus ExtractionStatus { get; private set; } = ExtractionStatus.NotApplicable;

    /// <summary>The Markdown read from a file; set only when <see cref="ExtractionStatus"/> is <c>Extracted</c>.</summary>
    public string? ExtractedText { get; private set; }

    /// <summary>Records the outcome of reading a file's text. Only files can carry it, and extracted text cannot be blank.</summary>
    public void ApplyExtraction(ExtractionStatus status, string? text)
    {
        if (Kind != AttachmentKind.File)
        {
            throw new DomainException("Solo un archivo puede tener texto extraído.");
        }

        if (status == ExtractionStatus.Extracted && string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("El texto extraído no puede estar vacío.");
        }

        ExtractionStatus = status;
        ExtractedText = status == ExtractionStatus.Extracted ? text : null;
    }

    /// <summary>The opaque key by which file storage finds the bytes; built from ids only, never from a name.</summary>
    public static string StorageKeyFor(Guid initiativeId, Guid id) => $"{initiativeId:N}/{id:N}";

    /// <summary>True when the last extension of the normalized name is on the blocklist (case-insensitive).</summary>
    public static bool IsBlockedExtension(string? fileName) =>
        BlockedExtensions.Contains(AttachmentFileName.ExtensionOf(AttachmentFileName.Normalize(fileName)));

    /// <summary>True for an absolute http or https URL with a host, trimmed length at most 2,000 characters.</summary>
    public static bool IsAllowedUrl(string? url)
    {
        var trimmed = url?.Trim() ?? string.Empty;

        return trimmed.Length is > 0 and <= UrlMaxLength
            && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host.Length > 0;
    }

    public static Attachment CreateFile(
        Guid id, Guid initiativeId, string ownerId, string? fileName, string? contentType, long sizeBytes, DateTimeOffset now)
    {
        var name = AttachmentFileName.Normalize(fileName);

        if (IsBlockedExtension(name))
        {
            throw new DomainException(BlockedFileMessage);
        }

        if (sizeBytes < 1)
        {
            throw new DomainException(EmptyFileMessage);
        }

        var type = string.IsNullOrWhiteSpace(contentType) ? DefaultContentType : contentType.Trim();

        return new()
        {
            Id = id,
            InitiativeId = initiativeId,
            OwnerId = ownerId,
            Kind = AttachmentKind.File,
            Name = name,
            ContentType = type.Length > ContentTypeMaxLength ? type[..ContentTypeMaxLength] : type,
            SizeBytes = sizeBytes,
            StorageKey = StorageKeyFor(initiativeId, id),
            CreatedAt = now,
        };
    }

    public static Attachment CreateLink(Guid initiativeId, string ownerId, string? url, DateTimeOffset now)
    {
        var trimmed = url?.Trim() ?? string.Empty;

        if (trimmed.Length > UrlMaxLength)
        {
            throw new DomainException(UrlTooLongMessage);
        }

        if (!IsAllowedUrl(trimmed))
        {
            throw new DomainException(InvalidUrlMessage);
        }

        return new()
        {
            Id = Guid.NewGuid(),
            InitiativeId = initiativeId,
            OwnerId = ownerId,
            Kind = AttachmentKind.Link,
            Url = trimmed,
            CreatedAt = now,
        };
    }

    public static Attachment CreateNote(Guid initiativeId, string ownerId, string? title, string? text, DateTimeOffset now)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        var trimmedText = text?.Trim() ?? string.Empty;

        if (trimmedTitle.Length == 0)
        {
            throw new DomainException(EmptyNoteTitleMessage);
        }

        if (trimmedTitle.Length > NoteTitleMaxLength)
        {
            throw new DomainException(NoteTitleTooLongMessage);
        }

        if (trimmedText.Length == 0)
        {
            throw new DomainException(EmptyNoteTextMessage);
        }

        if (trimmedText.Length > NoteTextMaxLength)
        {
            throw new DomainException(NoteTextTooLongMessage);
        }

        return new()
        {
            Id = Guid.NewGuid(),
            InitiativeId = initiativeId,
            OwnerId = ownerId,
            Kind = AttachmentKind.Note,
            Name = trimmedTitle,
            Text = trimmedText,
            CreatedAt = now,
        };
    }
}
