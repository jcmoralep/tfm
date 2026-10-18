using System.Text;
using RutaProducto.Application.Features.Context;
using RutaProducto.Application.Features.Context.GetContextForAssistant;
using RutaProducto.Domain.Context;

namespace RutaProducto.Application.Tests.Context;

public sealed class ContextExcerptTests
{
    private const string Marker = GetContextForAssistantQueryHandler.ClippedMarker;

    private const string WordMarkdown = "# Titulo - texto del Word";

    private readonly ContextTestContext context = new();

    [Fact]
    public async Task Only_notes_and_the_four_text_extensions_are_included_whatever_the_content_type()
    {
        var id = await context.Initiatives.CreateDraft("App");

        await context.AddNote(id, "Nota", "texto de la nota");
        await context.AddFile(id, "a.md", Utf8("markdown"), contentType: "application/pdf");
        await context.AddFile(id, "b.CSV", Utf8("a,b"), contentType: "image/png");
        await context.AddFile(id, "c.txt", Utf8("plano"), contentType: "video/mp4");
        await context.AddFile(id, "d.markdown", Utf8("md largo"));
        await context.AddFile(id, "d.pdf", Utf8("no"));
        await context.AddFile(id, "e.png", Utf8("no"));
        await context.AddFile(id, "f.mp4", Utf8("no"));
        await context.AddFile(id, "sinextension", Utf8("no"));
        await context.AddLink(id, "https://example.com/secreto");

        var excerpts = await context.Excerpts(id);

        Assert.Equal(["Nota", "a.md", "b.CSV", "c.txt", "d.markdown"], excerpts.Select(e => e.Title));
        Assert.Equal([AttachmentKind.Note, .. Enumerable.Repeat(AttachmentKind.File, 4)], excerpts.Select(e => e.Kind));
        Assert.Equal(["texto de la nota", "markdown", "a,b", "plano", "md largo"], excerpts.Select(e => e.Text));
    }

    [Theory]
    [InlineData(14_999, 14_999, false)]
    [InlineData(15_000, 15_000, false)]
    [InlineData(15_001, 15_000, true)]
    public async Task An_item_is_cut_at_fifteen_thousand_characters_and_only_then_ends_with_the_marker(int length, int expected, bool clipped)
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddFile(id, "grande.txt", Utf8(new string('x', length)));

        var excerpt = Assert.Single(await context.Excerpts(id));

        Assert.Equal(15_000, GetContextForAssistantQueryHandler.MaxItemCharacters);
        Assert.Equal(expected, excerpt.Text.Length);
        Assert.Equal(clipped, excerpt.Text.EndsWith(Marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_total_never_passes_sixty_thousand_the_oldest_are_kept_and_the_last_clipped_one_ends_with_the_marker()
    {
        var id = await context.Initiatives.CreateDraft("App");

        for (var index = 1; index <= 5; index++)
        {
            await context.AddFile(id, $"{index}.txt", Utf8(new string((char)('a' + index), 14_000)));
        }

        var excerpts = await context.Excerpts(id);

        Assert.Equal(60_000, GetContextForAssistantQueryHandler.MaxTotalCharacters);
        Assert.Equal(["1.txt", "2.txt", "3.txt", "4.txt", "5.txt"], excerpts.Select(e => e.Title));
        Assert.Equal(60_000, excerpts.Sum(e => e.Text.Length));
        Assert.Equal(4_000, excerpts[4].Text.Length);
        Assert.EndsWith(Marker, excerpts[4].Text);
        Assert.All(excerpts.Take(4), e => Assert.DoesNotContain(Marker, e.Text));
        Assert.StartsWith("b", excerpts[0].Text);
    }

    [Fact]
    public async Task A_file_with_extracted_text_is_sent_with_that_text_and_an_unreadable_or_untouched_one_is_not()
    {
        var id = await context.Initiatives.CreateDraft("App");
        var now = context.Clock.GetUtcNow();

        Attachment Word(string name, ExtractionStatus status, string? text, int minute)
        {
            var file = Attachment.CreateFile(Guid.NewGuid(), id, ContextTestContext.UserA, name, "application/octet-stream", 100, now.AddMinutes(minute));
            file.ApplyExtraction(status, text);

            return file;
        }

        await context.Attachments.AddAsync(Word("lee.docx", ExtractionStatus.Extracted, WordMarkdown, 1), default);
        await context.Attachments.AddAsync(Word("roto.docx", ExtractionStatus.Unreadable, null, 2), default);
        await context.Attachments.AddAsync(Word("viejo.doc", ExtractionStatus.NotApplicable, null, 3), default);
        await context.Attachments.AddAsync(Word("largo.docx", ExtractionStatus.Extracted, new string('w', 200_000), 4), default);

        var excerpts = await context.Excerpts(id);

        Assert.Equal(["lee.docx", "largo.docx"], excerpts.Select(e => e.Title));
        Assert.Equal(WordMarkdown, excerpts[0].Text);
        Assert.Equal(GetContextForAssistantQueryHandler.MaxItemCharacters, excerpts[1].Text.Length);
        Assert.EndsWith(Marker, excerpts[1].Text);
    }

    [Fact]
    public async Task At_most_ten_items_are_sent_oldest_first()
    {
        var id = await context.Initiatives.CreateDraft("App");

        for (var index = 1; index <= 12; index++)
        {
            await context.AddNote(id, $"N{index}", "x");
            context.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var excerpts = await context.Excerpts(id);

        Assert.Equal(Enumerable.Range(1, 10).Select(index => $"N{index}"), excerpts.Select(e => e.Title));
    }

    [Fact]
    public async Task Invalid_utf8_is_replaced_and_the_item_is_kept()
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddFile(id, "raro.txt", [0x68, 0x6F, 0xFF, 0xFE, 0x6C, 0x61]);

        var excerpt = Assert.Single(await context.Excerpts(id));

        Assert.StartsWith("ho", excerpt.Text);
        Assert.Contains('�', excerpt.Text);
        Assert.EndsWith("la", excerpt.Text);
    }

    [Fact]
    public async Task At_most_twenty_thousand_bytes_of_a_file_are_read()
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddFile(id, "enorme.md", Utf8(new string('a', 30_000) + "FINAL"));

        var excerpt = Assert.Single(await context.Excerpts(id));

        Assert.DoesNotContain("FINAL", excerpt.Text);
        Assert.Equal(GetContextForAssistantQueryHandler.MaxItemCharacters, excerpt.Text.Length);
    }

    [Fact]
    public async Task A_missing_or_unreadable_file_is_skipped_and_counted_without_an_exception()
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddNote(id, "Nota", "queda");
        await context.AddFile(id, "perdido.txt", Utf8("se pierde"));
        await context.AddFile(id, "roto.txt", Utf8("falla"));
        context.Storage.Files.Remove(context.Storage.Files.Keys.First());

        var excerpts = await context.Excerpts(id);

        Assert.Equal(["Nota", "roto.txt"], excerpts.Select(e => e.Title));

        context.Storage.FailOnOpen = new IOException("boom");
        var withoutFiles = await context.Excerpts(id);

        Assert.Equal(["Nota"], withoutFiles.Select(e => e.Title));
        Assert.Contains("2 skipped", context.Logs.Entries.Last(e => e.Message.StartsWith("Context for the assistant")).Message);
    }

    [Fact]
    public async Task A_foreign_or_unknown_initiative_gives_an_empty_list()
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddNote(id, "Nota", "texto");

        context.User.UserId = ContextTestContext.UserB;

        Assert.Empty(await context.Excerpts(id));
        Assert.Empty(await context.Excerpts(Guid.NewGuid()));
    }

    [Fact]
    public async Task The_logs_carry_the_counts_and_never_the_content()
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddNote(id, "TITULO-DISTINTIVO", "TEXTO-DISTINTIVO");
        await context.AddFile(id, "NOMBRE-DISTINTIVO.txt", Utf8("CONTENIDO-DISTINTIVO"));
        await context.AddFile(id, "PERDIDO-DISTINTIVO.md", Utf8("x"));
        context.Storage.Files.Remove(Attachment.StorageKeyFor(id, context.Attachments.Stored[2].Id));

        await context.Excerpts(id);

        var entry = Assert.Single(context.Logs.Entries, e => e.Message.StartsWith("Context for the assistant"));
        Assert.Contains("2 items sent, 1 skipped", entry.Message);
        Assert.DoesNotContain(context.Logs.Entries, e => e.AllText.Contains("DISTINTIVO", StringComparison.Ordinal));
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
