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
    public void Restore_ignores_quick_replies()
    {
        var draft = new ChatDraft();

        draft.Restore([QueuedAnswer.ForQuickReply("unknown", "No sé")]);

        Assert.Equal("", draft.Text);
    }
}
