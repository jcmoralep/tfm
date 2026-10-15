using RutaProducto.Domain.Common;
using RutaProducto.Domain.Context;

namespace RutaProducto.Application.Tests.Domain;

public sealed class AttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid InitiativeId = Guid.NewGuid();

    private static Attachment File(string name, long size = 1024, string? contentType = "application/pdf") =>
        Attachment.CreateFile(Guid.NewGuid(), InitiativeId, "user-a", name, contentType, size, Now);

    [Fact]
    public void A_file_keeps_its_metadata_and_gets_a_key_built_from_ids_only()
    {
        var id = Guid.NewGuid();

        var file = Attachment.CreateFile(id, InitiativeId, "user-a", "informe.pdf", "application/pdf", 2048, Now);

        Assert.Equal(AttachmentKind.File, file.Kind);
        Assert.Equal("informe.pdf", file.Name);
        Assert.Equal(2048, file.SizeBytes);
        Assert.Equal($"{InitiativeId:N}/{id:N}", file.StorageKey);
        Assert.Equal(Attachment.StorageKeyFor(InitiativeId, id), file.StorageKey);
        Assert.Null(file.Url);
        Assert.Null(file.Text);
    }

    [Theory]
    [InlineData("a.exe")]
    [InlineData("B.DLL")]
    [InlineData("c.bat")]
    [InlineData("d.cmd")]
    [InlineData("e.com")]
    [InlineData("f.msi")]
    [InlineData("g.scr")]
    [InlineData("h.js")]
    [InlineData("i.vbs")]
    [InlineData("j.ps1")]
    [InlineData("k.sh")]
    [InlineData("l.jar")]
    [InlineData("a.exe.")]
    [InlineData("a.EXE")]
    public void Blocklisted_extensions_are_rejected(string name)
    {
        Assert.True(Attachment.IsBlockedExtension(name));
        Assert.Throws<DomainException>(() => File(name));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("foto.png")]
    [InlineData("video.mp4")]
    [InlineData("archivo")]
    [InlineData("x.exe.txt")]
    public void Other_names_are_accepted(string name)
    {
        Assert.False(Attachment.IsBlockedExtension(name));
        Assert.Equal(name, File(name).Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_file_without_bytes_is_rejected(long size)
    {
        var error = Assert.Throws<DomainException>(() => File("a.pdf", size));

        Assert.Equal(Attachment.EmptyFileMessage, error.Message);
    }

    [Theory]
    [InlineData(null, "application/octet-stream")]
    [InlineData("  ", "application/octet-stream")]
    [InlineData("image/png", "image/png")]
    public void A_blank_content_type_becomes_octet_stream(string? sent, string expected)
    {
        Assert.Equal(expected, File("a.png", contentType: sent).ContentType);
    }

    [Fact]
    public void A_hostile_content_type_is_limited_to_255_characters()
    {
        Assert.Equal(255, File("a.png", contentType: new string('x', 400)).ContentType!.Length);
    }

    [Theory]
    [InlineData("https://example.com/a?b=1", "https://example.com/a?b=1")]
    [InlineData("HTTP://Example.com", "HTTP://Example.com")]
    [InlineData("  https://example.com  ", "https://example.com")]
    public void Valid_links_are_stored_trimmed_without_a_name(string url, string expected)
    {
        var link = Attachment.CreateLink(InitiativeId, "user-a", url, Now);

        Assert.True(Attachment.IsAllowedUrl(url));
        Assert.Equal(AttachmentKind.Link, link.Kind);
        Assert.Equal(expected, link.Url);
        Assert.Null(link.Name);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("file:///c:/x")]
    [InlineData("/relative/path")]
    [InlineData("example.com")]
    [InlineData("https://")]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejected_links_fail(string url)
    {
        Assert.False(Attachment.IsAllowedUrl(url));
        Assert.Throws<DomainException>(() => Attachment.CreateLink(InitiativeId, "user-a", url, Now));
    }

    [Fact]
    public void A_link_over_2000_characters_is_rejected_and_one_of_2000_passes()
    {
        var prefix = "https://example.com/";

        Assert.True(Attachment.IsAllowedUrl(prefix + new string('a', 2_000 - prefix.Length)));
        Assert.False(Attachment.IsAllowedUrl(prefix + new string('a', 2_001 - prefix.Length)));
        Assert.Throws<DomainException>(
            () => Attachment.CreateLink(InitiativeId, "user-a", prefix + new string('a', 2_001 - prefix.Length), Now));
    }

    [Fact]
    public void A_note_keeps_trimmed_title_and_text()
    {
        var note = Attachment.CreateNote(InitiativeId, "user-a", "  Idea  ", "  Texto\nlibre  ", Now);

        Assert.Equal(AttachmentKind.Note, note.Kind);
        Assert.Equal("Idea", note.Name);
        Assert.Equal("Texto\nlibre", note.Text);
        Assert.Null(note.StorageKey);
    }

    [Theory]
    [InlineData("", "texto")]
    [InlineData("   ", "texto")]
    [InlineData("titulo", "")]
    [InlineData("titulo", "   ")]
    [InlineData(121, "texto")]
    [InlineData("titulo", 5_001)]
    public void Invalid_notes_are_rejected(object title, object text)
    {
        Assert.Throws<DomainException>(() => Attachment.CreateNote(InitiativeId, "user-a", Value(title), Value(text), Now));
    }

    [Fact]
    public void A_note_at_the_limits_is_accepted()
    {
        var note = Attachment.CreateNote(InitiativeId, "user-a", new string('t', 120), new string('x', 5_000), Now);

        Assert.Equal(120, note.Name!.Length);
        Assert.Equal(5_000, note.Text!.Length);
    }

    // A number stands for a string of that many characters, so the Theory stays readable.
    private static string Value(object value) => value is int length ? new string('x', length) : (string)value;
}
