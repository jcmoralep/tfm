using RutaProducto.Application.Features.Context;
using RutaProducto.Domain.Context;
using RutaProducto.Web.Components.Context;

namespace RutaProducto.Web.Tests.Context;

public sealed class ContextLabelsTests
{
    [Theory]
    [InlineData(0L, "0 KB")]
    [InlineData(1L, "1 KB")]
    [InlineData(1023L, "1 KB")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "2 KB")]
    [InlineData(1572864L, "1,5 MB")]
    [InlineData(2516582L, "2,4 MB")]
    [InlineData(10485760L, "10 MB")]
    [InlineData(52428800L, "50 MB")]
    public void Sizes_are_shown_in_Spanish(long bytes, string expected) =>
        Assert.Equal(expected, ContextLabels.SizeLabel(bytes));

    [Theory]
    [InlineData("https://example.com/a?b=1", true)]
    [InlineData("HTTP://Example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,x", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("example.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_web_addresses_open_in_a_new_tab(string? url, bool expected) =>
        Assert.Equal(expected, ContextLabels.IsWebUrl(url));

    [Fact]
    public void A_file_row_shows_its_size_and_the_rest_only_their_kind()
    {
        var created = DateTimeOffset.UnixEpoch;
        var file = new AttachmentItem(Guid.NewGuid(), AttachmentKind.File, "a.pdf", null, null, 2048, created);
        var note = new AttachmentItem(Guid.NewGuid(), AttachmentKind.Note, "Idea", null, "x", null, created);

        Assert.Equal("Archivo · 2 KB", ContextLabels.RowDetail(file));
        Assert.Equal("Nota", ContextLabels.RowDetail(note));
    }

    [Theory]
    [InlineData(1, "Texto extraído: 1 carácter")]
    [InlineData(999, "Texto extraído: 999 caracteres")]
    [InlineData(12_345, "Texto extraído: 12.345 caracteres")]
    [InlineData(200_000, "Texto extraído: 200.000 caracteres")]
    public void The_extracted_line_writes_thousands_with_a_dot(int characters, string expected) =>
        Assert.Equal(expected, ContextLabels.ExtractedLine(characters));

    [Theory]
    [InlineData("informe.pdf", ContextLabels.PdfUnreadableLine)]
    [InlineData("INFORME.PDF", ContextLabels.PdfUnreadableLine)]
    [InlineData("informe.docx", ContextLabels.UnreadableLine)]
    [InlineData("pdf", ContextLabels.UnreadableLine)]
    [InlineData("informe.pdf.docx", ContextLabels.UnreadableLine)]
    public void An_unreadable_pdf_gets_the_scanned_document_hint_and_the_rest_the_generic_sentence(string fileName, string expected) =>
        Assert.Equal(expected, ContextLabels.UnreadableText(fileName));
}
