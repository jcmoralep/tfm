namespace RutaProducto.Domain.Artifacts;

/// <summary>A document is a draft until its owner approves it by hand; any content change returns it to Draft.</summary>
public enum ArtifactStatus
{
    Draft,
    Approved,
}
