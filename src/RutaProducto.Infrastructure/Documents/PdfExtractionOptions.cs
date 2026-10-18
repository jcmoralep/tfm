namespace RutaProducto.Infrastructure.Documents;

/// <summary>
/// Fixed safety limits for reading an untrusted PDF. They are not configuration keys (no settings screen or switch);
/// they are a record so tests can inject tiny values.
/// </summary>
internal sealed record PdfExtractionOptions(
    int MaxPages = 300,
    int MaxCharacters = 200_000,
    long MaxBytes = 100L * 1024 * 1024,
    TimeSpan? MaxDuration = null)
{
    /// <summary>Shorter than the 15 s the upload allows, so the extractor ends by itself before the caller gives up on it.</summary>
    public static readonly TimeSpan DefaultMaxDuration = TimeSpan.FromSeconds(10);

    public TimeSpan Duration => MaxDuration ?? DefaultMaxDuration;
}
