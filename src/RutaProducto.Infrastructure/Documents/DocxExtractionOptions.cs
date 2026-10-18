namespace RutaProducto.Infrastructure.Documents;

/// <summary>
/// Fixed safety limits for reading an untrusted .docx. They are not configuration keys (no settings screen or switch);
/// they are a record so tests can inject tiny values.
/// </summary>
internal sealed record DocxExtractionOptions(
    int MaxEntries = 2_000,
    long MaxUncompressedBytes = 100L * 1024 * 1024,
    int MaxCharacters = 200_000,
    int MaxXmlDepth = 200);
