using BmadPlatform.Application.Features.Context;
using BmadPlatform.Domain.Context;

namespace BmadPlatform.Application.Tests.Context;

public sealed class ContextHandlerTests
{
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
}
