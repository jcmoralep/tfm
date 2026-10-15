using RutaProducto.Application.Features.Context;
using RutaProducto.Application.Features.Context.AddFileAttachment;
using RutaProducto.Application.Features.Context.AddLinkAttachment;
using RutaProducto.Application.Features.Context.AddNoteAttachment;
using RutaProducto.Domain.Context;

namespace RutaProducto.Application.Tests.Context;

public sealed class ContextValidatorTests
{
    private const long TenMegabytes = 10L * 1024 * 1024;

    private static string[] FileErrors(string name, long size) =>
        [.. new AddFileAttachmentCommandValidator(new ContextLimits(TenMegabytes))
            .Validate(new AddFileAttachmentCommand(Guid.NewGuid(), name, null, size, Stream.Null)).Errors.Select(e => e.ErrorMessage)];

    private static string[] LinkErrors(string url) =>
        [.. new AddLinkAttachmentCommandValidator().Validate(new AddLinkAttachmentCommand(Guid.NewGuid(), url)).Errors.Select(e => e.ErrorMessage)];

    private static string[] NoteErrors(string title, string text) =>
        [.. new AddNoteAttachmentCommandValidator().Validate(new AddNoteAttachmentCommand(Guid.NewGuid(), title, text)).Errors.Select(e => e.ErrorMessage)];

    [Theory]
    [InlineData("a.pdf", 1)]
    [InlineData("notes.txt", TenMegabytes)]
    [InlineData("foto.png", 5)]
    [InlineData("video.mp4", 5)]
    [InlineData("archivo", 5)]
    [InlineData("x.exe.txt", 5)]
    public void Acceptable_files_pass(string name, long size) => Assert.Empty(FileErrors(name, size));

    [Theory]
    [InlineData("a.txt", 0, "El archivo está vacío.")]
    [InlineData("a.txt", -1, "El archivo está vacío.")]
    [InlineData("a.txt", TenMegabytes + 1, "El archivo supera el máximo de 10 MB.")]
    [InlineData("a.EXE", 5, "Este tipo de archivo no se puede adjuntar por seguridad.")]
    [InlineData("a.exe.", 5, "Este tipo de archivo no se puede adjuntar por seguridad.")]
    [InlineData("..\\x.ps1", 5, "Este tipo de archivo no se puede adjuntar por seguridad.")]
    public void Unacceptable_files_fail_with_a_spanish_message(string name, long size, string message) =>
        Assert.Equal([message], FileErrors(name, size));

    [Theory]
    [InlineData(".exe")]
    [InlineData(".dll")]
    [InlineData(".bat")]
    [InlineData(".cmd")]
    [InlineData(".com")]
    [InlineData(".msi")]
    [InlineData(".scr")]
    [InlineData(".js")]
    [InlineData(".vbs")]
    [InlineData(".ps1")]
    [InlineData(".sh")]
    [InlineData(".jar")]
    public void Every_blocked_extension_fails(string extension) =>
        Assert.Equal([Attachment.BlockedFileMessage], FileErrors("a" + extension, 5));

    [Fact]
    public void A_missing_stream_or_initiative_fails()
    {
        var validator = new AddFileAttachmentCommandValidator(new ContextLimits(TenMegabytes));

        Assert.False(validator.Validate(new AddFileAttachmentCommand(Guid.Empty, "a.txt", null, 5, null!)).IsValid);
    }

    [Theory]
    [InlineData("https://example.com/a?b=1")]
    [InlineData("HTTP://Example.com")]
    [InlineData("  https://example.com  ")]
    public void Valid_links_pass(string url) => Assert.Empty(LinkErrors(url));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///c:/x")]
    [InlineData("example.com")]
    [InlineData("/relativo")]
    [InlineData("https://")]
    [InlineData("")]
    public void Invalid_links_fail_with_the_link_message(string url) => Assert.Equal([Attachment.InvalidUrlMessage], LinkErrors(url));

    [Fact]
    public void A_link_of_2001_characters_fails_with_the_length_message()
    {
        const string Prefix = "https://example.com/";

        Assert.Empty(LinkErrors(Prefix + new string('a', 2000 - Prefix.Length)));
        Assert.Equal([Attachment.UrlTooLongMessage], LinkErrors(Prefix + new string('a', 2001 - Prefix.Length)));
    }

    [Theory]
    [InlineData(120, 5000)]
    [InlineData(1, 1)]
    public void Notes_at_the_limits_pass(int titleLength, int textLength) =>
        Assert.Empty(NoteErrors(new string('t', titleLength), new string('x', textLength)));

    [Theory]
    [InlineData("", "texto", "El título de la nota es obligatorio.")]
    [InlineData("   ", "texto", "El título de la nota es obligatorio.")]
    [InlineData("Nota", "  ", "El texto de la nota es obligatorio.")]
    public void Blank_note_fields_fail(string title, string text, string message) => Assert.Equal([message], NoteErrors(title, text));

    [Fact]
    public void Too_long_note_fields_fail()
    {
        Assert.Equal([Attachment.NoteTitleTooLongMessage], NoteErrors(new string('t', 121), "texto"));
        Assert.Equal([Attachment.NoteTextTooLongMessage], NoteErrors("Nota", new string('x', 5001)));
    }
}
