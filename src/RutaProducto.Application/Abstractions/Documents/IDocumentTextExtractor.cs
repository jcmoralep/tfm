using RutaProducto.Domain.Context;

namespace RutaProducto.Application.Abstractions.Documents;

/// <summary>
/// Reads the text of one kind of uploaded document and returns it as Markdown. One implementation per format
/// (.docx and .pdf); the upload picks the first one whose <see cref="Supports"/> accepts the file extension, so a new
/// format only adds a class and its registration. The input is untrusted: implementations must never write it to
/// disk, execute anything in it, or log its content.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>True when this extractor handles the extension (with the dot, any case, for example <c>.docx</c>).</summary>
    bool Supports(string extension);

    /// <summary>
    /// Extracts the text. It must not throw for a bad file: corrupt, protected, oversized or empty content gives
    /// <see cref="DocumentTextResult.Unreadable"/>. Cancellation is the only exception it may let through.
    /// </summary>
    Task<DocumentTextResult> ExtractAsync(Stream content, CancellationToken cancellationToken);
}

/// <summary>The outcome of an extraction: <c>Extracted</c> with Markdown, or <c>Unreadable</c> with none.</summary>
public sealed record DocumentTextResult(ExtractionStatus Status, string? Markdown)
{
    public static DocumentTextResult Unreadable { get; } = new(ExtractionStatus.Unreadable, null);

    public static DocumentTextResult Extracted(string markdown) => new(ExtractionStatus.Extracted, markdown);
}
