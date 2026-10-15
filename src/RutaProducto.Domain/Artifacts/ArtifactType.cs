namespace RutaProducto.Domain.Artifacts;

/// <summary>
/// The documents of the platform. Architecture and epics and stories are known values (stored as strings, no
/// migration) but no catalog entry offers them yet, so nothing can generate one.
/// </summary>
public enum ArtifactType
{
    Spec,
    Brief,
    Prd,
    Architecture,
    EpicsAndStories,
}
