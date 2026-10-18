using System.Text;
using RutaProducto.Application.Abstractions.Documents;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Domain.Context;

namespace RutaProducto.Application.Tests.Context;

public sealed class ContextExtractionTests
{
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("contenido del archivo");

    private readonly ScriptedDocumentTextExtractor extractor = new();

    [Theory]
    [InlineData("informe.docx")]
    [InlineData("INFORME.DOCX")]
    public async Task A_docx_is_extracted_after_it_is_stored_and_the_original_file_is_kept_as_is(string name)
    {
        var context = Context(extractor);
        var id = await context.Initiatives.CreateDraft("App");
        extractor.Result = DocumentTextResult.Extracted("# Titulo\n\ntexto");

        var view = await context.AddFile(id, name, Bytes);

        var stored = Assert.Single(context.Attachments.Stored);
        Assert.Equal(ExtractionStatus.Extracted, stored.ExtractionStatus);
        Assert.Equal("# Titulo\n\ntexto", stored.ExtractedText);
        Assert.Equal(Bytes, context.Storage.Files[stored.StorageKey!]);

        var item = Assert.Single(view.Items);
        Assert.Equal(ExtractionStatus.Extracted, item.ExtractionStatus);
        Assert.Equal("# Titulo\n\ntexto".Length, item.ExtractedCharacters);
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("a.doc")]
    [InlineData("a.docm")]
    [InlineData("a.txt")]
    [InlineData("a.docx.png")]
    [InlineData("sinextension")]
    public async Task Other_types_are_not_extracted_and_stay_not_applicable(string name)
    {
        var context = Context(extractor);
        var id = await context.Initiatives.CreateDraft("App");

        await context.AddFile(id, name, Bytes);

        var stored = Assert.Single(context.Attachments.Stored);
        Assert.Equal(ExtractionStatus.NotApplicable, stored.ExtractionStatus);
        Assert.Null(stored.ExtractedText);
        Assert.Equal(0, extractor.Calls);
    }

    [Theory]
    [InlineData("throws")]
    [InlineData("unreadable")]
    [InlineData("blank")]
    [InlineData("never-finishes")]
    [InlineData("storage-fails")]
    public async Task Any_failure_leaves_the_upload_successful_and_the_file_unreadable(string scenario)
    {
        var context = Context(extractor, TimeSpan.FromMilliseconds(100));
        var id = await context.Initiatives.CreateDraft("App");

        switch (scenario)
        {
            case "throws":
                extractor.Throws = new InvalidOperationException("boom");
                break;
            case "unreadable":
                extractor.Result = DocumentTextResult.Unreadable;
                break;
            case "blank":
                extractor.Result = DocumentTextResult.Extracted("   ");
                break;
            case "never-finishes":
                extractor.Blocks = new TaskCompletionSource().Task;
                break;
            default:
                context.Storage.FailOnOpen = new IOException("disco");
                break;
        }

        var view = await context.AddFile(id, "informe.docx", Bytes);

        var stored = Assert.Single(context.Attachments.Stored);
        Assert.Equal(ExtractionStatus.Unreadable, stored.ExtractionStatus);
        Assert.Null(stored.ExtractedText);
        Assert.Equal(Bytes, context.Storage.Files[stored.StorageKey!]);
        Assert.Equal(ExtractionStatus.Unreadable, Assert.Single(view.Items).ExtractionStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_extraction_log_has_counts_and_never_the_file_name_the_text_or_an_exception_message(bool fails)
    {
        var context = Context(extractor);
        var id = await context.Initiatives.CreateDraft("App");

        if (fails)
        {
            extractor.Throws = new InvalidOperationException("MENSAJE-DISTINTIVO");
        }
        else
        {
            extractor.Result = DocumentTextResult.Extracted("TEXTO-DISTINTIVO");
        }

        await context.AddFile(id, "NOMBRE-DISTINTIVO.docx", Bytes);

        var entry = Assert.Single(context.Logs.Entries, e => e.Message.StartsWith("Text extraction"));
        Assert.DoesNotContain(context.Logs.Entries, e => e.AllText.Contains("DISTINTIVO", StringComparison.Ordinal));

        if (fails)
        {
            Assert.Contains("Unreadable, 0 characters", entry.Message);
            Assert.Contains("InvalidOperationException", entry.Message);
        }
        else
        {
            Assert.Contains("Extracted, 16 characters", entry.Message);
        }
    }

    private static ContextTestContext Context(IDocumentTextExtractor extractor, TimeSpan? timeout = null) =>
        new(extractors: [extractor], extractionTimeout: timeout);
}
