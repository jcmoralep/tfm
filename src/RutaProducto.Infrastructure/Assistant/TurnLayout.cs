namespace RutaProducto.Infrastructure.Assistant;

/// <summary>
/// The one layout of a Question turn, shared by the deterministic fake and the model-worded turns so the stored text
/// has the same shape whoever wrote the words: acknowledgement, question, example, reason.
/// </summary>
internal static class TurnLayout
{
    public static string Compose(string acknowledgement, string prompt, string example, string why) =>
        $"{acknowledgement}{prompt}\n\nPor ejemplo: {example}\n\nPor qué lo pregunto: {why}";
}
