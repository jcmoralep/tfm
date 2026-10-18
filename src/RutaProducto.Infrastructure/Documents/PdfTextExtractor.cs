using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using RutaProducto.Application.Abstractions.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RutaProducto.Infrastructure.Documents;

/// <summary>
/// Reads the text of a PDF and writes it as plain Markdown paragraphs, page after page in reading order (PdfPig's
/// content-order extractor), pages separated by one blank line. Nothing is inferred: no headings, tables or lists.
/// The file is untrusted, so the page count is checked before any text is read, pages are read one at a time and
/// reading stops at the character cap, the time and the cancellation token are checked between pages, and the bytes
/// are only ever read from the given stream (copied to memory, within a byte limit, when it cannot seek), never
/// written to disk. Password-protected, corrupt, scanned (no text) or otherwise unreadable files are
/// <see cref="DocumentTextResult.Unreadable"/>; nothing here logs content.
/// </summary>
internal sealed partial class PdfTextExtractor(PdfExtractionOptions options) : IDocumentTextExtractor
{
    public const string TruncationMarker = DocxMarkdownExtractor.TruncationMarker;

    private const string BlockSeparator = "\n\n";

    public bool Supports(string extension) => string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<DocumentTextResult> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var markdown = Read(content, cancellationToken);

            return Task.FromResult(string.IsNullOrWhiteSpace(markdown) ? DocumentTextResult.Unreadable : DocumentTextResult.Extracted(markdown));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Corrupt, protected, not a PDF, over a limit, out of time, or whatever the parser throws on hostile input:
            // the caller only needs to know it is unreadable.
            return Task.FromResult(DocumentTextResult.Unreadable);
        }
    }

    private string Read(Stream content, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();

        using var copy = AsSeekable(content);

        var parsing = new ParsingOptions { SkipMissingFonts = true };
        var text = new ContentOrderTextExtractor.Options { SeparateParagraphsWithDoubleNewline = true, ReplaceWhitespaceWithSpace = true };

        using var document = PdfDocument.Open(copy ?? content, parsing);

        if (document.NumberOfPages > options.MaxPages)
        {
            throw new InvalidDataException("Too many pages.");
        }

        var output = new StringBuilder();

        for (var number = 1; number <= document.NumberOfPages; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (clock.Elapsed > options.Duration)
            {
                throw new TimeoutException("Reading took too long.");
            }

            var page = Normalize(ContentOrderTextExtractor.GetText(document.GetPage(number), text));

            if (page.Length == 0)
            {
                continue;
            }

            if (output.Length > 0)
            {
                output.Append(BlockSeparator);
            }

            output.Append(page);

            if (output.Length > options.MaxCharacters)
            {
                return Truncate(output);
            }
        }

        return output.ToString().Trim();
    }

    // PdfPig copies a stream that cannot seek without a limit; do it here, within the byte limit.
    private MemoryStream? AsSeekable(Stream content)
    {
        if (content.CanSeek)
        {
            return null;
        }

        var copy = new MemoryStream();

        using (var limited = new BudgetStream(content, new ByteBudget(options.MaxBytes)))
        {
            limited.CopyTo(copy);
        }

        copy.Position = 0;

        return copy;
    }

    // Spaces collapsed inside a line, blank lines collapsed to one, no leading or trailing blanks.
    private static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')
            .Select(line => Spaces().Replace(line, " ").Trim());

        var result = new StringBuilder();
        var pendingBlank = false;

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                pendingBlank = result.Length > 0;

                continue;
            }

            if (result.Length > 0)
            {
                result.Append(pendingBlank ? BlockSeparator : "\n");
            }

            result.Append(line);
            pendingBlank = false;
        }

        return result.ToString();
    }

    // The marker line counts toward the cap, so the result never exceeds it.
    private string Truncate(StringBuilder output)
    {
        var cut = Math.Max(options.MaxCharacters - TruncationMarker.Length - BlockSeparator.Length, 0);

        // Never end on half of a surrogate pair.
        if (cut > 0 && char.IsHighSurrogate(output[cut - 1]))
        {
            cut--;
        }

        return output.ToString(0, cut).TrimEnd() + BlockSeparator + TruncationMarker;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Spaces();
}
