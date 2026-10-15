using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Context.GetAttachmentDownload;
using BmadPlatform.Domain.Context;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.Context;

public sealed class ContextAccessTests
{
    private readonly ContextTestContext context = new();

    private async Task<(Guid InitiativeId, Guid FileId)> InitiativeWithAFile(string name = "a.txt", byte[]? bytes = null)
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddFile(id, name, bytes ?? [1, 2, 3]);
        context.Journal.Clear();

        return (id, context.Attachments.Stored.Single().Id);
    }

    [Fact]
    public async Task Deleting_a_file_removes_the_stored_file_and_then_the_row()
    {
        var (id, fileId) = await InitiativeWithAFile();

        var view = await context.Delete(id, fileId);

        Assert.Empty(view.Items);
        Assert.Empty(context.Storage.Files);
        Assert.Empty(context.Attachments.Stored);
        Assert.Equal(["file:delete", "row:delete"], context.Journal);
    }

    [Fact]
    public async Task A_storage_failure_still_removes_the_row_and_is_logged_with_ids_only()
    {
        var (id, fileId) = await InitiativeWithAFile("zz-nombre-secreto.txt");
        context.Storage.FailOnDelete = new IOException("zz-nombre-secreto.txt");

        await context.Delete(id, fileId);

        Assert.Empty(context.Attachments.Stored);
        var warning = Assert.Single(context.Logs.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(fileId.ToString(), warning.AllText);
        Assert.Contains(nameof(IOException), warning.AllText);
        Assert.DoesNotContain("zz-", warning.AllText);
    }

    [Fact]
    public async Task A_file_that_is_already_missing_does_not_block_the_delete()
    {
        var (id, fileId) = await InitiativeWithAFile();
        context.Storage.Files.Clear();

        await context.Delete(id, fileId);

        Assert.Empty(context.Attachments.Stored);
    }

    [Fact]
    public async Task Links_and_notes_are_deleted_without_touching_storage()
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddLink(id, "https://example.com");
        await context.AddNote(id, "Nota", "texto");

        foreach (var row in context.Attachments.Stored.ToList())
        {
            await context.Delete(id, row.Id);
        }

        Assert.Empty(context.Attachments.Stored);
        Assert.Equal(0, context.Storage.DeleteCalls);
    }

    [Fact]
    public async Task A_foreign_unknown_or_other_initiatives_attachment_cannot_be_deleted()
    {
        var (id, fileId) = await InitiativeWithAFile();
        var other = await context.Initiatives.CreateDraft("Otra");

        // Same owner, but the attachment belongs to another initiative; then an unknown id.
        await Assert.ThrowsAsync<NotFoundException>(() => context.Delete(other, fileId));
        await Assert.ThrowsAsync<NotFoundException>(() => context.Delete(id, Guid.NewGuid()));

        context.User.UserId = ContextTestContext.UserB;
        var error = await Assert.ThrowsAsync<NotFoundException>(() => context.Delete(id, fileId));

        Assert.Equal("La iniciativa no existe.", error.Message);
        Assert.Single(context.Attachments.Stored);
        Assert.Single(context.Storage.Files);
    }

    [Fact]
    public async Task An_own_file_downloads_with_its_name_and_bytes()
    {
        var (id, fileId) = await InitiativeWithAFile("informe final.pdf", [9, 8, 7]);

        var download = (await context.Download(id, fileId))!;
        using var content = new MemoryStream();
        await download.Content.CopyToAsync(content);
        await download.Content.DisposeAsync();

        Assert.Equal("informe final.pdf", download.FileName);
        Assert.Equal(new byte[] { 9, 8, 7 }, content.ToArray());
    }

    [Theory]
    [InlineData("foreign-user")]
    [InlineData("other-initiative")]
    [InlineData("deleted-initiative")]
    [InlineData("unknown-attachment")]
    [InlineData("link")]
    [InlineData("note")]
    [InlineData("missing-object")]
    public async Task Everything_that_is_not_an_own_downloadable_file_gives_null(string scenario)
    {
        var (id, fileId) = await InitiativeWithAFile();
        await context.AddLink(id, "https://example.com");
        await context.AddNote(id, "Nota", "texto");
        var linkId = context.Attachments.Stored.Single(a => a.Kind == AttachmentKind.Link).Id;
        var noteId = context.Attachments.Stored.Single(a => a.Kind == AttachmentKind.Note).Id;
        var other = await context.Initiatives.CreateDraft("Otra");

        var result = scenario switch
        {
            "foreign-user" => await AsUserB(() => context.Download(id, fileId)),
            "other-initiative" => await context.Download(other, fileId),
            "deleted-initiative" => await DeleteInitiativeThenDownload(id, fileId),
            "unknown-attachment" => await context.Download(id, Guid.NewGuid()),
            "link" => await context.Download(id, linkId),
            "note" => await context.Download(id, noteId),
            _ => await MissingObject(id, fileId),
        };

        Assert.Null(result);
    }

    [Fact]
    public async Task No_log_entry_contains_a_name_title_url_or_text_on_any_path()
    {
        var id = await context.Initiatives.CreateDraft("App");

        await context.AddFile(id, "zz-nombre-secreto.txt", [1, 2], contentType: "zz/tipo-secreto");
        await context.AddLink(id, "https://zz-host-secreto.example/zz-ruta");
        await context.AddNote(id, "zz-titulo-secreto", "zz-texto-secreto");
        await context.Get(id);
        var fileId = context.Attachments.Stored.Single(a => a.Kind == AttachmentKind.File).Id;
        await (await context.Download(id, fileId))!.Content.DisposeAsync();

        // Failure paths: a storage failure on delete, a missing object, and a failed insert with a failed cleanup.
        context.Storage.FailOnDelete = new IOException("zz-nombre-secreto.txt zz-texto-secreto");
        await context.Delete(id, fileId);
        context.Storage.Files.Clear();
        context.Attachments.FailOnAdd = new InvalidOperationException("zz-titulo-secreto");
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.AddFile(id, "zz-nombre-secreto.txt", [1]));

        Assert.NotEmpty(context.Logs.Entries);
        Assert.All(context.Logs.Entries, entry => Assert.DoesNotContain("zz-", entry.AllText));
    }

    private async Task<T> AsUserB<T>(Func<Task<T>> action)
    {
        context.User.UserId = ContextTestContext.UserB;

        return await action();
    }

    private async Task<AttachmentDownload?> DeleteInitiativeThenDownload(Guid id, Guid fileId)
    {
        await context.Initiatives.Delete(id);

        return await context.Download(id, fileId);
    }

    private async Task<AttachmentDownload?> MissingObject(Guid id, Guid fileId)
    {
        context.Storage.Files.Clear();

        var result = await context.Download(id, fileId);
        Assert.Contains(context.Logs.Entries, e => e.Level == LogLevel.Warning && e.AllText.Contains(fileId.ToString()));

        return result;
    }
}
