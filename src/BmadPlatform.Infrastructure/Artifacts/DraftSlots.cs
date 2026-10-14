namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// The slots of a document the model can fill. A slot fed by a topic uses the topic key as its id; the others
/// (sections no topic feeds) have the ids below.
/// </summary>
internal static class DraftSlots
{
    public const string Differentiator = "differentiator";
    public const string ScopeInside = "scope-inside";
    public const string Vision = "vision";
}
