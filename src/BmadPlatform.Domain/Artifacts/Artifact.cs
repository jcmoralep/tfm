using BmadPlatform.Domain.Common;

namespace BmadPlatform.Domain.Artifacts;

/// <summary>
/// One Markdown document of an initiative, one per initiative and type. <see cref="Version"/> counts content
/// changes only (it is not a history) and doubles as the concurrency token; approving never changes it. Every
/// mutator receives the current time so the aggregate never reads the clock.
/// </summary>
public sealed class Artifact
{
    public const int ContentMaxLength = 20_000;

    public const string EmptyContentMessage = "El documento no puede estar vacío.";

    public const string TooLongMessage = "El documento no puede superar los 20000 caracteres.";

    // Required by EF Core to materialize the entity.
    private Artifact()
    {
        OwnerId = string.Empty;
        Content = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid InitiativeId { get; private set; }

    public string OwnerId { get; private set; }

    public ArtifactType Type { get; private set; }

    public int Version { get; private set; }

    public ArtifactStatus Status { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>True while the document is approved; read it before a mutation to know the status it had.</summary>
    public bool WasApproved => Status == ArtifactStatus.Approved;

    public static Artifact Create(Guid initiativeId, string ownerId, ArtifactType type, string content, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            InitiativeId = initiativeId,
            OwnerId = ownerId,
            Type = type,
            Version = 1,
            Status = ArtifactStatus.Draft,
            Content = NormalizeContent(content),
            CreatedAt = now,
            UpdatedAt = now,
        };

    /// <summary>Regeneration: replaces the whole content, raises the version and returns the document to Draft.</summary>
    public void Replace(string content, DateTimeOffset now)
    {
        Content = NormalizeContent(content);
        Version++;
        Status = ArtifactStatus.Draft;
        ApprovedAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Manual edit. Returns false and changes nothing when the content is identical after normalization; otherwise
    /// raises the version, returns the document to Draft and clears the approval.
    /// </summary>
    public bool Edit(string content, DateTimeOffset now)
    {
        var normalized = NormalizeContent(content);

        if (string.Equals(normalized, Content, StringComparison.Ordinal))
        {
            return false;
        }

        Replace(normalized, now);

        return true;
    }

    /// <summary>Approves a draft without touching the version. Approving an approved document is a no-op.</summary>
    public void Approve(DateTimeOffset now)
    {
        if (Status == ArtifactStatus.Approved)
        {
            return;
        }

        Status = ArtifactStatus.Approved;
        ApprovedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Unifies line endings and trims surrounding whitespace. The content rules are not applied.</summary>
    public static string Normalize(string? content) =>
        (content ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    /// <summary>Normalizes, then enforces the content rules.</summary>
    public static string NormalizeContent(string? content)
    {
        var normalized = Normalize(content);

        if (normalized.Length == 0)
        {
            throw new DomainException(EmptyContentMessage);
        }

        if (normalized.Length > ContentMaxLength)
        {
            throw new DomainException(TooLongMessage);
        }

        return normalized;
    }
}
