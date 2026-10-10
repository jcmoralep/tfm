namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>
/// Turns the conversation answers into the Markdown of one document. The result must pass the content rules of
/// the domain (not blank, at most <see cref="Domain.Artifacts.Artifact.ContentMaxLength"/> characters), use the
/// identifier scheme of <see cref="Markdown.ArtifactOutline"/> and carry no frontmatter.
/// </summary>
public interface IArtifactDrafter
{
    Task<string> DraftAsync(DraftRequest request, CancellationToken cancellationToken);
}
