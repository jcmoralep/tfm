using System.Globalization;
using System.Text;
using RutaProducto.Application.Abstractions.Documents;
using RutaProducto.Domain.Context;
using RutaProducto.Infrastructure.Documents;

namespace RutaProducto.Infrastructure.Tests.Documents;

public sealed class PdfTextExtractorTests
{
    [Fact]
    public async Task Pages_are_read_in_order_with_one_blank_line_between_them_and_whitespace_collapsed()
    {
        var bytes = Pdf(
            ["Primera   pagina", "segunda linea"],
            ["Segunda pagina"],
            ["Tercera pagina"]);

        var result = await Extract(bytes);

        Assert.Equal(ExtractionStatus.Extracted, result.Status);
        Assert.Equal(
            string.Join('\n', "Primera pagina", "segunda linea", string.Empty, "Segunda pagina", string.Empty, "Tercera pagina"),
            result.Markdown);
    }

    [Fact]
    public async Task Accents_pipes_and_hashes_are_kept_as_plain_text_and_nothing_becomes_a_heading_or_a_table()
    {
        var bytes = Pdf(["Camión: 3 | 4 # tareas ñandú"], ["Título"]);

        var result = await Extract(bytes);

        Assert.Equal("Camión: 3 | 4 # tareas ñandú\n\nTítulo", result.Markdown);
    }

    [Fact]
    public async Task A_very_long_document_is_cut_at_the_cap_and_ends_with_the_marker_line()
    {
        var pages = Enumerable.Range(0, 20)
            .Select(page => Enumerable.Range(0, 30).Select(line => $"Pagina {page} linea {line} " + new string('x', 40)).ToArray())
            .ToArray();

        var result = await Extract(Pdf(pages), new PdfExtractionOptions(MaxCharacters: 5_000));

        Assert.Equal(ExtractionStatus.Extracted, result.Status);
        Assert.True(result.Markdown!.Length <= 5_000);
        Assert.EndsWith("\n\n" + PdfTextExtractor.TruncationMarker, result.Markdown);
        Assert.StartsWith("Pagina 0 linea 0 ", result.Markdown);
    }

    [Theory]
    [InlineData("no-text")]
    [InlineData("empty-bytes")]
    [InlineData("header-only")]
    [InlineData("corrupted")]
    [InlineData("not-a-pdf")]
    [InlineData("too-many-pages")]
    [InlineData("out-of-time")]
    public async Task A_file_without_readable_text_or_over_a_limit_is_unreadable_and_never_throws(string scenario)
    {
        var options = new PdfExtractionOptions();
        var good = Pdf(["uno"], ["dos"], ["tres"]);
        byte[] bytes;

        switch (scenario)
        {
            case "no-text":
                bytes = Pdf([], []);
                break;
            case "empty-bytes":
                bytes = [];
                break;
            case "header-only":
                bytes = Encoding.ASCII.GetBytes("%PDF-1.4");
                break;
            case "corrupted":
                bytes = [.. good[..(good.Length / 2)], .. new byte[] { 0, 255, 7, 7, 7 }];
                break;
            case "not-a-pdf":
                bytes = Encoding.UTF8.GetBytes("Esto es solo texto, no un PDF.");
                break;
            case "too-many-pages":
                bytes = good;
                options = new PdfExtractionOptions(MaxPages: 2);
                break;
            default:
                bytes = good;
                options = new PdfExtractionOptions(MaxDuration: TimeSpan.FromTicks(-1));
                break;
        }

        var result = await Extract(bytes, options);

        Assert.Equal(ExtractionStatus.Unreadable, result.Status);
        Assert.Null(result.Markdown);
    }

    [Fact]
    public async Task A_stream_that_cannot_seek_is_read_too_and_a_cancelled_token_is_the_only_thing_that_throws()
    {
        var bytes = Pdf(["texto"], ["otro"]);
        var extractor = new PdfTextExtractor(new PdfExtractionOptions());

        var result = await extractor.ExtractAsync(new ForwardOnlyStream(bytes), default);

        Assert.Equal("texto\n\notro", result.Markdown);
        Assert.True(extractor.Supports(".PDF"));
        Assert.False(extractor.Supports(".docx"));

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractAsync(new MemoryStream(bytes), cancelled.Token));
    }

    [Fact]
    public async Task Cancelling_while_reading_pages_stops_the_extraction_with_a_cancellation()
    {
        using var cancel = new CancellationTokenSource();
        var extractor = new PdfTextExtractor(new PdfExtractionOptions());

        // Cancelled by the stream itself the first time it is read, so the token is already set before any page.
        var stream = new CancelOnReadStream(Pdf(["uno"], ["dos"], ["tres"]), cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractAsync(stream, cancel.Token));
    }

    private static Task<DocumentTextResult> Extract(byte[] bytes, PdfExtractionOptions? options = null) =>
        new PdfTextExtractor(options ?? new PdfExtractionOptions()).ExtractAsync(new MemoryStream(bytes), default);

    // One array of lines per page, written top to bottom in Helvetica with WinAnsi encoding (so accents work without
    // embedding a font). Hand-written PDF 1.4: catalog, page tree, font, then a page and a content stream per page.
    private static byte[] Pdf(params string[][] pages)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Count {pages.Length} /Kids [{string.Join(' ', pages.Select((_, index) => $"{4 + (index * 2)} 0 R"))}] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        };

        foreach (var (lines, index) in pages.Select((lines, index) => (lines, index)))
        {
            var text = string.Concat(lines.Select(line => $"({Literal(line)}) Tj T* "));
            var content = $"BT /F1 11 Tf 14 TL 40 800 Td {text}ET";

            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + (index * 2)} 0 R >>");
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        var output = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.Latin1.GetByteCount(output.ToString()));
            output.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xref = Encoding.Latin1.GetByteCount(output.ToString());

        output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");

        foreach (var offset in offsets)
        {
            output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        return Encoding.Latin1.GetBytes(output.ToString());
    }

    private static string Literal(string line) => line.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);

    private sealed class CancelOnReadStream(byte[] bytes, CancellationTokenSource cancel) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            cancel.Cancel();

            return base.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            cancel.Cancel();

            return base.Read(buffer);
        }
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
