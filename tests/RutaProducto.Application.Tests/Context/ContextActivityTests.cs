using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;

namespace RutaProducto.Application.Tests.Context;

/// <summary>Adding or deleting an attachment notes activity on its initiative after it succeeded, and only then.</summary>
public sealed class ContextActivityTests
{
    private readonly ContextTestContext context = new();

    private int Recorded() => context.Sender.Sent.OfType<RecordInitiativeActivityCommand>().Count();

    [Fact]
    public async Task Each_kind_of_attachment_notes_activity_once_after_it_is_stored()
    {
        var id = await context.Initiatives.CreateDraft("App");
        var itemsWhenNoted = new List<int>();
        context.Sender.BeforeHandle = request =>
        {
            if (request is RecordInitiativeActivityCommand)
            {
                itemsWhenNoted.Add(context.Attachments.Stored.Count);
            }
        };
        var before = Recorded();

        await context.AddLink(id, "https://example.com/a");
        await context.AddNote(id, "Nota", "texto");
        await context.AddFile(id, "a.txt", [1, 2, 3]);

        Assert.Equal(before + 3, Recorded());
        Assert.Equal([1, 2, 3], itemsWhenNoted);
    }

    [Fact]
    public async Task Deleting_notes_activity_after_the_row_is_gone_and_not_for_a_missing_attachment()
    {
        var id = await context.Initiatives.CreateDraft("App");
        await context.AddLink(id, "https://example.com/a");
        var attachmentId = context.Attachments.Stored.Single().Id;
        var rowsWhenNoted = -1;
        context.Sender.BeforeHandle = request =>
        {
            if (request is RecordInitiativeActivityCommand)
            {
                rowsWhenNoted = context.Attachments.Stored.Count;
            }
        };
        var before = Recorded();

        await Assert.ThrowsAsync<NotFoundException>(() => context.Delete(id, Guid.NewGuid()));
        Assert.Equal(before, Recorded());

        await context.Delete(id, attachmentId);

        Assert.Equal(before + 1, Recorded());
        Assert.Equal(0, rowsWhenNoted);
    }

    [Fact]
    public async Task A_refused_attachment_notes_no_activity()
    {
        var unknown = Guid.NewGuid();
        var before = Recorded();

        await Assert.ThrowsAsync<NotFoundException>(() => context.AddLink(unknown, "https://example.com/a"));
        await Assert.ThrowsAsync<NotFoundException>(() => context.AddNote(unknown, "Nota", "texto"));
        await Assert.ThrowsAsync<NotFoundException>(() => context.AddFile(unknown, "a.txt", [1]));

        Assert.Equal(before, Recorded());
    }

    [Fact]
    public async Task A_failure_while_noting_activity_never_fails_the_attachment()
    {
        var id = await context.Initiatives.CreateDraft("App");
        context.Sender.BeforeHandle = request =>
        {
            if (request is RecordInitiativeActivityCommand)
            {
                throw new InvalidOperationException("boom");
            }
        };

        var view = await context.AddLink(id, "https://example.com/a");

        Assert.Single(view.Items);
    }
}
