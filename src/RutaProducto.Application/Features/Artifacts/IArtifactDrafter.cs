namespace RutaProducto.Application.Features.Artifacts;

/// <summary>
/// Turns the conversation answers into the Markdown of one document. The result must pass the content rules of
/// the domain (not blank, at most <see cref="Domain.Artifacts.Artifact.ContentMaxLength"/> characters), use the
/// identifier scheme of <see cref="Markdown.ArtifactOutline"/> and carry no frontmatter.
/// <para>
/// Failure contract: a drafter that is unavailable or that exceeds its time limit throws
/// <see cref="Common.Exceptions.AssistantUnavailableException"/>, whose message is shown to the person as it is. The
/// use cases let it through unchanged and have stored nothing by then. The drafter must honor the cancellation token.
/// </para>
/// </summary>
public interface IArtifactDrafter
{
    Task<string> DraftAsync(DraftRequest request, CancellationToken cancellationToken);
}
