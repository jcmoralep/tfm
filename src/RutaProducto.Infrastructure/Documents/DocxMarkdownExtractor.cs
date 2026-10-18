using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RutaProducto.Application.Abstractions.Documents;

namespace RutaProducto.Infrastructure.Documents;

/// <summary>
/// Reads the text of a Word .docx and writes it as Markdown: headings, paragraphs, lists, tables and the visible text
/// of hyperlinks. Images, comments, headers, footers and macros are ignored; tracked insertions are kept and deletions
/// dropped. The file is untrusted, so before parsing the zip is inspected (entry count, bytes really decompressed, XML
/// depth, no DTD) and the bytes are only ever read from memory or the given stream, never written to disk. Anything
/// that cannot be read, or has no text, is <see cref="DocumentTextResult.Unreadable"/>; nothing here logs content.
/// </summary>
internal sealed partial class DocxMarkdownExtractor(DocxExtractionOptions options) : IDocumentTextExtractor
{
    public const string TruncationMarker = "[…texto recortado…]";

    private const string BlockSeparator = "\n\n";

    private const int MaxListLevel = 8;

    public bool Supports(string extension) => string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase);

    public Task<DocumentTextResult> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        try
        {
            using var copy = AsSeekable(content);
            var stream = (Stream?)copy ?? content;

            Inspect(stream, cancellationToken);

            var markdown = Convert(stream, cancellationToken);

            return Task.FromResult(string.IsNullOrWhiteSpace(markdown) ? DocumentTextResult.Unreadable : DocumentTextResult.Extracted(markdown));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Corrupt, protected, not a zip, over a limit, unexpected markup: the caller only needs to know it is unreadable.
            return Task.FromResult(DocumentTextResult.Unreadable);
        }
    }

    // A zip needs random access; a stream that cannot seek is copied to memory within the same byte limit.
    private MemoryStream? AsSeekable(Stream content)
    {
        if (content.CanSeek)
        {
            return null;
        }

        var copy = new MemoryStream();

        using (var limited = new BudgetStream(content, new ByteBudget(options.MaxUncompressedBytes)))
        {
            limited.CopyTo(copy);
        }

        copy.Position = 0;

        return copy;
    }

    // Throws InvalidDataException (or a parse error) for anything unsafe or unreadable.
    private void Inspect(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        if (archive.Entries.Count > options.MaxEntries)
        {
            throw new InvalidDataException("Too many entries.");
        }

        if (archive.Entries.Sum(entry => entry.Length) > options.MaxUncompressedBytes)
        {
            throw new InvalidDataException("Too large once uncompressed.");
        }

        // The declared sizes can lie, so every entry is also counted while it is really decompressed.
        var budget = new ByteBudget(options.MaxUncompressedBytes);
        var buffer = new byte[81_920];

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            using var entryStream = new BudgetStream(entry.Open(), budget);

            if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            {
                ScanXml(entryStream, cancellationToken);
            }
            else
            {
                while (entryStream.Read(buffer, 0, buffer.Length) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }
    }

    // No DTD (entity expansion), no external resolution, and a depth the recursive parser can safely walk.
    private void ScanXml(Stream stream, CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false };

        using var reader = XmlReader.Create(stream, settings);

        while (reader.Read())
        {
            if (reader.Depth > options.MaxXmlDepth)
            {
                throw new InvalidDataException("The markup is nested too deeply.");
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private string Convert(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;

        using var document = WordprocessingDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = options.MaxUncompressedBytes });

        var main = document.MainDocumentPart ?? throw new InvalidDataException("No main document part.");
        var body = main.Document?.Body ?? throw new InvalidDataException("No document body.");
        var writer = new MarkdownWriter(options.MaxCharacters, new DocumentStyles(main));

        foreach (var block in Blocks(body))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!writer.Add(block))
            {
                break;
            }
        }

        return writer.ToMarkdown();
    }

    // Body children in order, looking inside block-level content controls.
    private static IEnumerable<OpenXmlElement> Blocks(OpenXmlElement parent)
    {
        foreach (var child in parent.ChildElements)
        {
            switch (child)
            {
                case Paragraph or Table:
                    yield return child;
                    break;
                case SdtBlock { SdtContentBlock: { } inner }:
                    foreach (var nested in Blocks(inner))
                    {
                        yield return nested;
                    }

                    break;
            }
        }
    }

    // Visible text of a paragraph in document order: inserted text counts, deleted and moved-away text does not.
    private static string ParagraphText(OpenXmlElement paragraph)
    {
        var text = new StringBuilder();

        foreach (var element in paragraph.Descendants())
        {
            switch (element)
            {
                case Text run when !run.Ancestors<DeletedRun>().Any() && !run.Ancestors<MoveFromRun>().Any():
                    text.Append(run.Text);
                    break;
                case TabChar or Break or CarriageReturn:
                    text.Append(' ');
                    break;
            }
        }

        return Whitespace().Replace(text.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^heading\s*([1-6])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HeadingName();

    /// <summary>Style names and list formats of the document, looked up once.</summary>
    private sealed class DocumentStyles(MainDocumentPart main)
    {
        private readonly Dictionary<string, string> styleNames = (main.StyleDefinitionsPart?.Styles?.Elements<Style>() ?? [])
            .Where(style => style.StyleId?.Value is not null)
            .GroupBy(style => style.StyleId!.Value!)
            .ToDictionary(group => group.Key, group => group.First().StyleName?.Val?.Value ?? string.Empty);

        private readonly Dictionary<(int NumberId, int Level), bool> bullets = [];

        /// <summary>1 to 6 for the Heading 1 to 6 styles (found by style name, so localized Word works), else 0.</summary>
        public int HeadingLevel(Paragraph paragraph)
        {
            var id = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;

            if (id is null)
            {
                return 0;
            }

            foreach (var candidate in new[] { styleNames.GetValueOrDefault(id), id })
            {
                if (candidate is not null && HeadingName().Match(candidate) is { Success: true } match)
                {
                    return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                }
            }

            return 0;
        }

        /// <summary>The list a paragraph belongs to: level 0 to 8 and whether it is a bullet, or null when it is not a list item.</summary>
        public (int Level, bool Bullet)? ListItem(Paragraph paragraph)
        {
            var numbering = paragraph.ParagraphProperties?.NumberingProperties;
            var numberId = numbering?.NumberingId?.Val?.Value;

            if (numberId is null or 0)
            {
                return null;
            }

            var level = Math.Clamp(numbering?.NumberingLevelReference?.Val?.Value ?? 0, 0, MaxListLevel);

            if (!bullets.TryGetValue((numberId.Value, level), out var bullet))
            {
                bullet = IsBullet(numberId.Value, level);
                bullets[(numberId.Value, level)] = bullet;
            }

            return (level, bullet);
        }

        private bool IsBullet(int numberId, int level)
        {
            var definitions = main.NumberingDefinitionsPart?.Numbering;
            var abstractId = definitions?.Elements<NumberingInstance>()
                .FirstOrDefault(instance => instance.NumberID?.Value == numberId)?.AbstractNumId?.Val?.Value;
            var format = definitions?.Elements<AbstractNum>()
                .FirstOrDefault(abstractNum => abstractNum.AbstractNumberId?.Value == abstractId)?
                .Elements<Level>()
                .FirstOrDefault(candidate => candidate.LevelIndex?.Value == level)?.NumberingFormat?.Val?.Value;

            // An unknown list is shown as bullets: the order is still the document order.
            return format is null || format == NumberFormatValues.Bullet || format == NumberFormatValues.None;
        }
    }

    /// <summary>Builds the Markdown block by block and stops at the character cap.</summary>
    private sealed class MarkdownWriter(int maxCharacters, DocumentStyles styles)
    {
        private readonly StringBuilder output = new();
        private readonly int[] counters = new int[MaxListLevel + 1];
        private bool previousWasListItem;
        private bool truncated;

        /// <summary>Adds one paragraph or table; false once the cap has been passed.</summary>
        public bool Add(OpenXmlElement block)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    AddParagraph(paragraph);
                    break;
                case Table table:
                    AddTable(table);
                    break;
            }

            if (output.Length > maxCharacters)
            {
                truncated = true;

                return false;
            }

            return true;
        }

        public string ToMarkdown()
        {
            if (!truncated)
            {
                return output.ToString().Trim();
            }

            var cut = Math.Max(maxCharacters - TruncationMarker.Length - BlockSeparator.Length, 0);

            // Never end on half of a surrogate pair.
            if (cut > 0 && char.IsHighSurrogate(output[cut - 1]))
            {
                cut--;
            }

            return output.ToString(0, cut).TrimEnd() + BlockSeparator + TruncationMarker;
        }

        private void AddParagraph(Paragraph paragraph)
        {
            var text = ParagraphText(paragraph);

            if (text.Length == 0)
            {
                return;
            }

            var heading = styles.HeadingLevel(paragraph);

            if (heading > 0)
            {
                Reset();
                Append($"{new string('#', heading)} {text}", isListItem: false);

                return;
            }

            if (styles.ListItem(paragraph) is not var (level, bullet))
            {
                Reset();
                Append(text, isListItem: false);

                return;
            }

            // An item at a shallower level restarts the numbering of the deeper ones.
            for (var deeper = level + 1; deeper <= MaxListLevel; deeper++)
            {
                counters[deeper] = 0;
            }

            var marker = bullet ? "-" : $"{++counters[level]}.";

            Append($"{new string(' ', level * 2)}{marker} {text}", isListItem: true);
        }

        private void AddTable(Table table)
        {
            var rows = table.Elements<TableRow>()
                .Select(row => row.Elements<TableCell>().Select(CellText).ToList())
                .Where(cells => cells.Count > 0)
                .ToList();

            if (rows.Count == 0)
            {
                return;
            }

            var columns = rows.Max(cells => cells.Count);
            var lines = new List<string>();

            for (var index = 0; index < rows.Count; index++)
            {
                lines.Add(Row(rows[index], columns));

                if (index == 0)
                {
                    lines.Add("|" + string.Concat(Enumerable.Repeat(" --- |", columns)));
                }
            }

            Reset();
            Append(string.Join('\n', lines), isListItem: false);
        }

        private static string Row(List<string> cells, int columns) =>
            "| " + string.Join(" | ", cells.Concat(Enumerable.Repeat(string.Empty, columns - cells.Count))) + " |";

        // Paragraphs of a cell (including nested tables) joined on one line, with the pipe escaped.
        private static string CellText(TableCell cell) =>
            string.Join(' ', cell.Descendants<Paragraph>().Select(ParagraphText).Where(text => text.Length > 0))
                .Replace("|", "\\|", StringComparison.Ordinal);

        private void Reset() => Array.Clear(counters);

        private void Append(string block, bool isListItem)
        {
            if (output.Length > 0)
            {
                output.Append(previousWasListItem && isListItem ? "\n" : BlockSeparator);
            }

            output.Append(block);
            previousWasListItem = isListItem;
        }
    }
}
