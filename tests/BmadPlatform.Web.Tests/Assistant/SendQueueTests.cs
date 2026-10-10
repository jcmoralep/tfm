using BmadPlatform.Web.Components.Assistant;

namespace BmadPlatform.Web.Tests.Assistant;

public sealed class SendQueueTests
{
    private static QueuedAnswer Text(string text) => QueuedAnswer.ForText(text);

    [Fact]
    public void A_new_queue_has_no_work()
    {
        var queue = new SendQueue();

        Assert.False(queue.HasWork);
        Assert.False(queue.IsSending);
        Assert.Null(queue.StartNext());
    }

    [Fact]
    public void Answers_go_out_in_the_order_typed_one_at_a_time()
    {
        var queue = new SendQueue();
        queue.Enqueue(Text("uno"));
        queue.Enqueue(Text("dos"));
        queue.Enqueue(Text("tres"));

        Assert.Equal("uno", queue.StartNext()!.Text);
        Assert.Null(queue.StartNext());

        queue.Complete();
        Assert.Equal("dos", queue.StartNext()!.Text);
        queue.Complete();
        Assert.Equal("tres", queue.StartNext()!.Text);
        queue.Complete();

        Assert.Null(queue.StartNext());
        Assert.False(queue.HasWork);
    }

    [Fact]
    public void The_queue_has_work_while_one_is_in_flight_even_when_nothing_waits()
    {
        var queue = new SendQueue();
        queue.Enqueue(Text("uno"));

        queue.StartNext();

        Assert.True(queue.HasWork);
        Assert.True(queue.IsSending);
        Assert.Equal(0, queue.WaitingCount);
    }

    [Fact]
    public void Outstanding_lists_the_one_in_flight_first_then_those_waiting()
    {
        var queue = new SendQueue();
        queue.Enqueue(Text("uno"));
        queue.Enqueue(Text("dos"));
        queue.StartNext();

        Assert.Equal(["uno", "dos"], queue.Outstanding.Select(a => a.Display));
    }

    [Fact]
    public void A_paused_queue_keeps_its_answers_until_resumed()
    {
        var queue = new SendQueue();
        queue.Enqueue(Text("uno"));
        queue.Enqueue(Text("dos"));
        queue.StartNext();
        queue.Complete();

        queue.Pause();

        Assert.True(queue.HasWork);
        Assert.Null(queue.StartNext());

        queue.Resume();

        Assert.Equal("dos", queue.StartNext()!.Text);
    }

    [Fact]
    public void Abort_returns_everything_unsent_in_order_and_empties_the_queue()
    {
        var queue = new SendQueue();
        queue.Enqueue(Text("uno"));
        queue.Enqueue(Text("dos"));
        queue.Enqueue(Text("tres"));
        queue.StartNext();
        queue.Pause();

        var dropped = queue.Abort();

        Assert.Equal(["uno", "dos", "tres"], dropped.Select(a => a.Text));
        Assert.False(queue.HasWork);
        Assert.False(queue.IsPaused);
    }

    [Fact]
    public void A_quick_reply_carries_its_key_and_shows_its_label()
    {
        var queue = new SendQueue();
        queue.Enqueue(QueuedAnswer.ForQuickReply("unknown", "No sé"));

        var next = queue.StartNext()!;

        Assert.Null(next.Text);
        Assert.Equal("unknown", next.QuickReplyKey);
        Assert.Equal("No sé", next.Display);
    }

    [Fact]
    public void An_answer_with_both_or_neither_text_and_key_is_refused()
    {
        var queue = new SendQueue();

        Assert.Throws<ArgumentException>(() => queue.Enqueue(new QueuedAnswer("a", "k", "a")));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(new QueuedAnswer(null, null, "")));
    }
}
