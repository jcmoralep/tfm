using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Context;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Context;
using BmadPlatform.Domain.Initiatives;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.Context;

public sealed class ContextHandlerTests
{
    private const long Megabyte = 1024 * 1024;

    private readonly ContextTestContext context = new();

    [Fact]
    public async Task The_view_lists_the_items_oldest_first_with_the_limits_and_the_usage()
    {
        var id = await context.Initiatives.CreateDraft("App");
        var now = context.Clock.GetUtcNow();

        // Added out of order on purpose: the order comes from CreatedAt, not from insertion.
        await context.Attachments.AddAsync(Attachment.CreateNote(id, ContextTestContext.UserA, "Nota", "texto", now.AddMinutes(2)), default);
        await context.Attachments.AddAsync(Attachment.CreateFile(Guid.NewGuid(), id, ContextTestContext.UserA, "a.pdf", "application/pdf", 2048, now), default);
        await context.Attachments.AddAsync(Attachment.CreateLink(id, ContextTestContext.UserA, "https://example.com/a?b=1", now.AddMinutes(1)), default);

        var view = (await context.Get(id))!;

        Assert.Equal(["a.pdf", "example.com", "Nota"], view.Items.Select(i => i.Label));
        Assert.Equal([AttachmentKind.File, AttachmentKind.Link, AttachmentKind.Note], view.Items.Select(i => i.Kind));
        Assert.Equal(2048, view.TotalBytes);
        Assert.Equal(context.Limits, view.Limits);
        Assert.True(view.CanAdd);
    }

    [Fact]
    public async Task An_initiative_with_no_context_gives_an_empty_view_and_a_foreign_one_gives_null()
    {
        var id = await context.Initiatives.CreateDraft("App");

        var empty = (await context.Get(id))!;

        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalBytes);

        context.User.UserId = ContextTestContext.UserB;

        Assert.Null(await context.Get(id));
        Assert.Null(await context.Get(Guid.NewGuid()));
    }

    [Fact]
    public async Task A_valid_file_is_stored_under_its_key_and_listed()
    {
        var id = await context.Initiatives.CreateDraft("App");
        var bytes = new byte[1024];
        Array.Fill(bytes, (byte)7);

        var view = await context.AddFile(id, "informe final.pdf", bytes, contentType: "application/pdf");

        var row = Assert.Single(context.Attachments.Stored);
        Assert.Equal("informe final.pdf", row.Name);
        Assert.Equal(1024, row.SizeBytes);
        Assert.Equal(bytes, context.Storage.Files[Attachment.StorageKeyFor(id, row.Id)]);
        Assert.Equal(["informe final.pdf"], view.Items.Select(i => i.Label));
        Assert.Equal(1024, view.TotalBytes);
    }

    [Fact]
    public async Task Path_parts_of_the_name_are_dropped()
    {
        var id = await context.Initiatives.CreateDraft("App");

        await context.AddFile(id, @"..\..\Windows\evil.txt", [1]);

        Assert.Equal("evil.txt", Assert.Single(context.Attachments.Stored).Name);
    }

    [Fact]
    public async Task A_stream_longer_than_announced_is_rejected_with_the_per_file_message_and_leaves_no_file()
    {
        var small = new ContextTestContext(maxFileBytes: 1024);
        var id = await small.Initiatives.CreateDraft("App");

        var error = await Assert.ThrowsAsync<DomainException>(() => small.AddFile(id, "a.txt", new byte[2048], declaredSize: 10));

        Assert.Equal(ContextTexts.FileTooLarge(1024), error.Message);
        Assert.Empty(small.Storage.Files);
        Assert.Empty(small.Attachments.Stored);
    }

    [Fact]
    public async Task A_real_zero_byte_stream_is_rejected_and_its_file_is_removed()
    {
        var id = await context.Initiatives.CreateDraft("App");

        var error = await Assert.ThrowsAsync<DomainException>(() => context.AddFile(id, "a.txt", [], declaredSize: 5));

        Assert.Equal(Attachment.EmptyFileMessage, error.Message);
        Assert.Empty(context.Storage.Files);
        Assert.Empty(context.Attachments.Stored);
    }

    // 49 MB are already used, so 1 MB is left for the total cap.
    [Theory]
    [InlineData(2, 2, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    public async Task The_total_cap_applies_to_the_announced_and_to_the_real_size(int announcedMegabytes, int realMegabytes, bool accepted)
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.Attachments.AddAsync(
            Attachment.CreateFile(Guid.NewGuid(), id, ContextTestContext.UserA, "seed.bin", null, 49 * Megabyte, context.Clock.GetUtcNow()),
            default);

        var attempt = () => context.AddFile(id, "b.bin", new byte[realMegabytes * Megabyte], declaredSize: announcedMegabytes * Megabyte);

        if (accepted)
        {
            Assert.Equal(2, (await attempt()).Items.Count);
        }
        else
        {
            var error = await Assert.ThrowsAsync<DomainException>(attempt);
            Assert.Equal(ContextTexts.TotalTooLarge, error.Message);
            Assert.Empty(context.Storage.Files);
        }
    }

    [Fact]
    public async Task The_twenty_first_item_of_any_kind_is_rejected()
    {
        var id = await context.Initiatives.CreateDraft("App");

        for (var i = 0; i < ContextLimits.MaxItems; i++)
        {
            await context.AddNote(id, $"Nota {i}", "texto");
        }

        Assert.False((await context.Get(id))!.CanAdd);
        Assert.Equal(ContextTexts.TooManyItems, (await Assert.ThrowsAsync<DomainException>(() => context.AddFile(id, "a.txt", [1]))).Message);
        Assert.Equal(ContextTexts.TooManyItems, (await Assert.ThrowsAsync<DomainException>(() => context.AddLink(id, "https://example.com"))).Message);
        Assert.Equal(ContextTexts.TooManyItems, (await Assert.ThrowsAsync<DomainException>(() => context.AddNote(id, "x", "y"))).Message);
        Assert.Equal(ContextLimits.MaxItems, context.Attachments.Stored.Count);
        Assert.Empty(context.Storage.Files);
    }

    [Fact]
    public async Task A_failed_insert_removes_the_stored_file_and_rethrows()
    {
        var id = await context.Initiatives.CreateDraft("App");
        context.Attachments.FailOnAdd = new InvalidOperationException("insert failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.AddFile(id, "a.txt", [1, 2]));

        Assert.Empty(context.Storage.Files);
        Assert.Equal(["file:save", "file:delete"], context.Journal);
    }

    [Fact]
    public async Task A_failed_cleanup_after_a_failed_insert_is_logged_with_ids_only_and_the_original_error_wins()
    {
        var id = await context.Initiatives.CreateDraft("App");
        context.Attachments.FailOnAdd = new InvalidOperationException("insert failed");
        context.Storage.FailOnDelete = new IOException("secret-name.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.AddFile(id, "secret-name.txt", [1, 2]));

        var warning = Assert.Single(context.Logs.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(id.ToString(), warning.AllText);
        Assert.DoesNotContain("secret-name", warning.AllText);
    }

    [Theory]
    [InlineData(InitiativeStatus.Draft)]
    [InlineData(InitiativeStatus.Planning)]
    [InlineData(InitiativeStatus.ReadyToBuild)]
    public async Task Context_can_be_added_in_any_status_without_changing_it(InitiativeStatus status)
    {
        var id = await context.Initiatives.CreateDraft("App");
        context.Initiatives.ForceStatus(id, status);

        await context.AddFile(id, "a.txt", [1]);
        await context.AddLink(id, "https://example.com");
        await context.AddNote(id, "Nota", "texto");

        Assert.Equal(3, context.Attachments.Stored.Count);
        Assert.Equal(status, (await context.Initiatives.Get(id))!.Status);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("link")]
    [InlineData("note")]
    public async Task Another_users_or_a_deleted_or_an_unknown_initiative_is_a_uniform_not_found_and_nothing_is_stored(string kind)
    {
        var mine = await context.Initiatives.CreateDraft("App");
        var deleted = await context.Initiatives.CreateDraft("Vieja");
        await context.Initiatives.Delete(deleted);

        Task Add(Guid id) => kind switch
        {
            "file" => context.AddFile(id, "a.txt", [1]),
            "link" => context.AddLink(id, "https://example.com"),
            _ => context.AddNote(id, "Nota", "texto"),
        };

        context.User.UserId = ContextTestContext.UserB;

        foreach (var id in new[] { mine, deleted, Guid.NewGuid() })
        {
            var error = await Assert.ThrowsAsync<NotFoundException>(() => Add(id));
            Assert.Equal("La iniciativa no existe.", error.Message);
        }

        Assert.Empty(context.Attachments.Stored);
        Assert.Empty(context.Storage.Files);
    }
}
