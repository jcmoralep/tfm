using RutaProducto.Domain.Artifacts;

namespace RutaProducto.Application.Features.Artifacts.ExportArtifact;

/// <summary>
/// Turns a stored document into a downloadable file. The stored Markdown stays the only source of truth; a new
/// format is a new implementation of this interface (RNF-07). The PDF is not one of them: the page prints itself.
/// </summary>
public interface IArtifactExporter
{
    ArtifactExport Export(ArtifactExportSource source);
}

public sealed record ArtifactExportSource(
    string InitiativeName,
    ArtifactType Type,
    ArtifactStatus Status,
    int Version,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public override string ToString() =>
        $"{nameof(ArtifactExportSource)} {{ Type = {Type}, Version = {Version}, Status = {Status}, Content = [redacted] }}";
}
