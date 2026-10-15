using RutaProducto.Domain.Artifacts;
using MediatR;

namespace RutaProducto.Application.Features.Artifacts.SaveArtifactContent;

/// <summary>
/// Replaces the Markdown of a document with the owner's edit. <paramref name="ExpectedVersion"/> is the version the
/// page was showing; a stale one is rejected. Saving the same text again is a no-op.
/// </summary>
public sealed record SaveArtifactContentCommand(Guid InitiativeId, ArtifactType Type, int ExpectedVersion, string Content)
    : IRequest<ArtifactView>
{
    // The synthesized ToString would print the document, which can contain initiative information.
    public override string ToString() =>
        $"{nameof(SaveArtifactContentCommand)} {{ InitiativeId = {InitiativeId}, Type = {Type}, " +
        $"ExpectedVersion = {ExpectedVersion}, Content = [redacted] }}";
}
