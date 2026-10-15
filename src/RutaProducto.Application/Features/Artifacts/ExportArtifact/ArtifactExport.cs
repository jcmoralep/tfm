namespace RutaProducto.Application.Features.Artifacts.ExportArtifact;

/// <summary>A document ready to download: the file name, its media type and its text.</summary>
public sealed record ArtifactExport(string FileName, string ContentType, string Content)
{
    // The synthesized ToString would print the document, which can contain initiative information.
    public override string ToString() => $"{nameof(ArtifactExport)} {{ FileName = {FileName}, Content = [redacted] }}";
}
