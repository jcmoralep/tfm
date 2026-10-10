using BmadPlatform.Web.Components.Assistant;

namespace BmadPlatform.Web.Tests.Assistant;

public sealed class ChatDraftTests
{
    [Fact]
    public void Submit_trims_queues_and_clears_the_composer()
    {
        var queue = new SendQueue();
        var draft = new ChatDraft { Text = "  Un portal para clientes \n" };

        var result = draft.TrySubmit(queue);

        Assert.Equal(DraftSubmission.Queued, result);
        Assert.Equal("", draft.Text);
        Assert.Equal("Un portal para clientes", queue.StartNext()!.Text);
    }

    [Fact]
    public void Typing_does_not_change_the_revision_but_replacing_the_text_does()
    {
        var draft = new ChatDraft();

        draft.Text = "Hola";
        Assert.Equal(0, draft.Revision);

        draft.Replace("");
        Assert.Equal(1, draft.Revision);
        Assert.Equal("", draft.Text);
    }

    [Fact]
    public void Submitting_and_restoring_change_the_revision()
    {
        var queue = new SendQueue();
        var draft = new ChatDraft { Text = "Una idea" };

        draft.TrySubmit(queue);
        var afterSubmit = draft.Revision;
        draft.Restore(queue.Abort());

        Assert.Equal(1, afterSubmit);
        Assert.Equal(2, draft.Revision);
    }

    [Fact]
    public void A_double_submit_stores_the_answer_once()
    {
        var queue = new SendQueue();
        var draft = new ChatDraft { Text = "Respuesta" };

        var first = draft.TrySubmit(queue);
        var second = draft.TrySubmit(queue);

        Assert.Equal(DraftSubmission.Queued, first);
        Assert.Equal(DraftSubmission.Empty, second);
        Assert.Equal(1, queue.WaitingCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void Whitespace_is_ignored(string text)
    {
        var queue = new SendQueue();
        var draft = new ChatDraft { Text = text };

        Assert.False(draft.HasContent);
        Assert.Equal(DraftSubmission.Empty, draft.TrySubmit(queue));
        Assert.False(queue.HasWork);
    }

    [Fact]
    public void Exactly_2000_characters_after_trimming_are_accepted()
    {
        var queue = new SendQueue();
        var draft = new ChatDraft { Text = "  " + new string('a', 2000) + "  " };

        Assert.False(draft.IsTooLong);
        Assert.Equal(DraftSubmission.Queued, draft.TrySubmit(queue));
        Assert.Equal(2000, queue.StartNext()!.Text!.Length);
    }

    [Fact]
    public void More_than_2000_characters_are_kept_in_the_composer()
    {
        var queue = new SendQueue();
        var text = new string('a', 2001);
        var draft = new ChatDraft { Text = text };

        Assert.True(draft.IsTooLong);
        Assert.Equal(DraftSubmission.TooLong, draft.TrySubmit(queue));
        Assert.Equal(text, draft.Text);
        Assert.False(queue.HasWork);
    }

    [Theory]
    [InlineData("", "0 / 2.000")]
    [InlineData("hola", "4 / 2.000")]
    public void The_counter_shows_the_length_and_the_limit(string text, string expected)
    {
        Assert.Equal(expected, new ChatDraft { Text = text }.Counter);
    }

    [Fact]
    public void Restore_puts_unsent_texts_back_in_order_ahead_of_new_typing()
    {
        var draft = new ChatDraft { Text = "algo nuevo" };

        draft.Restore([QueuedAnswer.ForText("uno"), QueuedAnswer.ForText("dos")]);

        Assert.Equal("uno\n\ndos\n\nalgo nuevo", draft.Text);
    }

    [Fact]
    public void Restore_keeps_the_text_of_an_answer_that_was_not_stored()
    {
        var draft = new ChatDraft();

        draft.Restore([QueuedAnswer.ForText("Mi respuesta")]);

        Assert.Equal("Mi respuesta", draft.Text);
    }

    [Fact]
    public void Restore_reports_the_quick_replies_it_cannot_put_in_the_box()
    {
        var draft = new ChatDraft();

        var restoration = draft.Restore([QueuedAnswer.ForQuickReply("unknown", "No sé"), QueuedAnswer.ForText("texto")]);

        Assert.Equal("texto", draft.Text);
        Assert.Equal(["No sé"], restoration.DroppedChoices);
        Assert.False(restoration.TooLong);
    }

    [Fact]
    public void Restore_reports_nothing_when_only_text_comes_back()
    {
        var restoration = new ChatDraft().Restore([QueuedAnswer.ForText("uno")]);

        Assert.Empty(restoration.DroppedChoices);
        Assert.False(restoration.TooLong);
    }

    [Fact]
    public void Restore_keeps_the_joined_text_editable_and_flags_it_when_it_passes_the_limit()
    {
        var draft = new ChatDraft { Text = new string('b', 1500) };

        var restoration = draft.Restore([QueuedAnswer.ForText(new string('a', 1000))]);

        Assert.Equal(1000 + 2 + 1500, draft.Text.Length);
        Assert.True(draft.IsTooLong);
        Assert.True(restoration.TooLong);
        Assert.Equal(DraftSubmission.TooLong, draft.TrySubmit(new SendQueue()));
    }
}
