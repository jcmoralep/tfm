using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RutaProducto.Application.Abstractions.Documents;
using RutaProducto.Domain.Context;
using RutaProducto.Infrastructure.Documents;

namespace RutaProducto.Infrastructure.Tests.Documents;

public sealed class DocxMarkdownExtractorTests
{
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [Fact]
    public async Task Headings_paragraphs_and_lists_become_markdown_with_whitespace_collapsed()
    {
        var bytes = Docx((main, body) =>
        {
            AddStyles(main);
            AddNumbering(main);
            body.Append(
                P("Titulo", style: "Heading1"),
                P("Parrafo   con \t espacios raros"),
                P("   "),
                P(string.Empty),
                P("Sub", style: "Ttulo2"),
                P("uno", numberId: 1),
                P("dos", numberId: 1, level: 1),
                P("a", numberId: 2),
                P("b", numberId: 2),
                P("fin"));
        });

        var result = await Extract(bytes);

        Assert.Equal(ExtractionStatus.Extracted, result.Status);
        Assert.Equal(
            string.Join('\n', "# Titulo", string.Empty, "Parrafo con espacios raros", string.Empty, "## Sub", string.Empty, "- uno", "  - dos", "1. a", "2. b", string.Empty, "fin"),
            result.Markdown);
    }

    [Fact]
    public async Task A_table_becomes_a_markdown_table_with_the_pipe_escaped_and_cell_lines_collapsed()
    {
        var bytes = Docx((_, body) => body.Append(new Table(
            Row(Cell("Nombre"), Cell("Nota|x")),
            Row(Cell("Ana"), Cell("l1", "l2")))));

        var result = await Extract(bytes);

        Assert.Equal(
            string.Join('\n', "| Nombre | Nota\\|x |", "| --- | --- |", "| Ana | l1 l2 |"),
            result.Markdown);
    }

    [Fact]
    public async Task Links_keep_their_text_tracked_changes_keep_insertions_and_headers_and_comments_are_ignored()
    {
        var bytes = Docx((main, body) =>
        {
            var link = main.AddHyperlinkRelationship(new Uri("https://example.com/secreto"), true);

            main.AddNewPart<HeaderPart>().Header = new Header(new Paragraph(new Run(new Text("ENCABEZADO"))));
            main.AddNewPart<WordprocessingCommentsPart>().Comments = new Comments(
                new Comment(new Paragraph(new Run(new Text("COMENTARIO")))) { Id = "0", Author = "x" });

            body.Append(
                new Paragraph(
                    Run("Hola "),
                    new Hyperlink(Run("sitio visible")) { Id = link.Id },
                    new InsertedRun(Run(" nuevo")) { Id = "1", Author = "a" },
                    new DeletedRun(new Run(new DeletedText(" viejo") { Space = SpaceProcessingModeValues.Preserve })) { Id = "2", Author = "a" },
                    Run(" fin")));
        });

        var result = await Extract(bytes);

        Assert.Equal("Hola sitio visible nuevo fin", result.Markdown);
    }

    [Fact]
    public async Task A_very_long_document_is_cut_at_the_cap_and_ends_with_the_marker_line()
    {
        var bytes = Docx((_, body) =>
        {
            for (var index = 0; index < 5_000; index++)
            {
                body.Append(P($"Parrafo numero {index} " + new string('x', 50)));
            }
        });

        var result = await Extract(bytes);

        Assert.Equal(ExtractionStatus.Extracted, result.Status);
        Assert.True(result.Markdown!.Length <= 200_000);
        Assert.EndsWith("\n\n" + DocxMarkdownExtractor.TruncationMarker, result.Markdown);
        Assert.StartsWith("Parrafo numero 0 ", result.Markdown);
    }

    [Theory]
    [InlineData("corrupted")]
    [InlineData("not-a-zip")]
    [InlineData("password-protected")]
    [InlineData("empty-bytes")]
    [InlineData("zip-without-document")]
    [InlineData("empty-document")]
    [InlineData("dtd")]
    [InlineData("too-deep")]
    public async Task Unreadable_files_give_unreadable_and_never_throw(string scenario)
    {
        var valid = Docx((_, body) => body.Append(P("texto")));
        var bytes = scenario switch
        {
            "corrupted" => valid[..(valid.Length / 2)],
            "not-a-zip" => Encoding.UTF8.GetBytes("esto no es un documento de Word"),

            // Word's password protection wraps the file in an OLE compound file, not a zip.
            "password-protected" => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0],
            "empty-bytes" => [],
            "zip-without-document" => ZipOf(("hola.txt", "hola")),
            "empty-document" => Docx((_, body) => body.Append(P(string.Empty), P("  "))),
            "dtd" => ReplaceEntry(valid, "word/document.xml", $"<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY a \"b\">]><w:document xmlns:w=\"{WordNamespace}\"><w:body><w:p><w:r><w:t>&a;</w:t></w:r></w:p></w:body></w:document>"),
            _ => ReplaceEntry(valid, "word/document.xml", $"<w:document xmlns:w=\"{WordNamespace}\"><w:body>{string.Concat(Enumerable.Repeat("<w:p>", 300))}{string.Concat(Enumerable.Repeat("</w:p>", 300))}</w:body></w:document>"),
        };

        var result = await Extract(bytes);

        Assert.Equal(ExtractionStatus.Unreadable, result.Status);
        Assert.Null(result.Markdown);
    }

    [Theory]
    [InlineData(3, 100_000_000)]
    [InlineData(2_000, 500)]
    public async Task A_zip_over_the_entry_or_size_limit_is_unreadable_while_the_same_file_passes_with_the_defaults(int maxEntries, long maxBytes)
    {
        var bytes = Docx((main, body) =>
        {
            AddStyles(main);
            body.Append(P("texto", style: "Heading1"));
        });

        Assert.Equal(ExtractionStatus.Extracted, (await Extract(bytes)).Status);
        Assert.Equal(
            ExtractionStatus.Unreadable,
            (await Extract(bytes, new DocxExtractionOptions(MaxEntries: maxEntries, MaxUncompressedBytes: maxBytes))).Status);
    }

    [Fact]
    public async Task A_stream_that_cannot_seek_is_read_too_and_a_cancelled_token_is_the_only_thing_that_throws()
    {
        var bytes = Docx((_, body) => body.Append(P("texto")));
        var extractor = new DocxMarkdownExtractor(new DocxExtractionOptions());

        var result = await extractor.ExtractAsync(new ForwardOnlyStream(bytes), default);

        Assert.Equal("texto", result.Markdown);
        Assert.True(extractor.Supports(".DOCX"));
        Assert.False(extractor.Supports(".doc"));

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractAsync(new MemoryStream(bytes), cancelled.Token));
    }

    private static Task<DocumentTextResult> Extract(byte[] bytes, DocxExtractionOptions? options = null) =>
        new DocxMarkdownExtractor(options ?? new DocxExtractionOptions()).ExtractAsync(new MemoryStream(bytes), default);

    private static byte[] Docx(Action<MainDocumentPart, Body> build)
    {
        using var memory = new MemoryStream();

        using (var document = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body();

            main.Document = new Document(body);
            build(main, body);
        }

        return memory.ToArray();
    }

    private static Run Run(string text) => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static Paragraph P(string text, string? style = null, int? numberId = null, int level = 0)
    {
        var properties = new ParagraphProperties();

        if (style is not null)
        {
            properties.Append(new ParagraphStyleId { Val = style });
        }

        if (numberId is not null)
        {
            properties.Append(new NumberingProperties(new NumberingLevelReference { Val = level }, new NumberingId { Val = numberId }));
        }

        return new Paragraph(properties, Run(text));
    }

    private static TableRow Row(params TableCell[] cells) => new(cells);

    private static TableCell Cell(params string[] lines) => new(lines.Select(line => P(line)));

    private static void AddStyles(MainDocumentPart main) =>
        main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(
            new Style(new StyleName { Val = "heading 1" }) { Type = StyleValues.Paragraph, StyleId = "Heading1" },

            // A Spanish Word names the style id after its localized name but keeps the canonical "heading 2".
            new Style(new StyleName { Val = "heading 2" }) { Type = StyleValues.Paragraph, StyleId = "Ttulo2" });

    private static void AddNumbering(MainDocumentPart main) =>
        main.AddNewPart<NumberingDefinitionsPart>().Numbering = new Numbering(
            new AbstractNum(
                new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }) { LevelIndex = 0 },
                new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }) { LevelIndex = 1 }) { AbstractNumberId = 1 },
            new AbstractNum(new Level(new NumberingFormat { Val = NumberFormatValues.Decimal }) { LevelIndex = 0 }) { AbstractNumberId = 2 },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 },
            new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = 2 });

    private static byte[] ZipOf(params (string Name, string Content)[] entries)
    {
        using var memory = new MemoryStream();

        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return memory.ToArray();
    }

    private static byte[] ReplaceEntry(byte[] docx, string entryName, string content)
    {
        using var memory = new MemoryStream();
        memory.Write(docx);

        using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, leaveOpen: true))
        {
            zip.GetEntry(entryName)!.Delete();

            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        return memory.ToArray();
    }

    private sealed class ForwardOnlyStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
