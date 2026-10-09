namespace BmadPlatform.Web.Components.Assistant;

/// <summary>An answer waiting to be sent: free text or the key of a quick reply, plus what the bubble shows meanwhile.</summary>
public sealed record QueuedAnswer(string? Text, string? QuickReplyKey, string Display)
{
    public static QueuedAnswer ForText(string text) => new(text, null, text);

    public static QueuedAnswer ForQuickReply(string key, string label) => new(null, key, label);
}

/// <summary>
/// The chat's send queue (confirmed decision). The domain only accepts an answer when the assistant's question is
/// the last visible message, so a send made while a reply is in flight waits here and goes out, in the order typed,
/// after the previous reply is stored. Not thread-safe: Blazor runs a circuit's events one at a time.
/// </summary>
public sealed class SendQueue
{
    private readonly Queue<QueuedAnswer> waiting = new();
    private QueuedAnswer? inFlight;

    /// <summary>True while the queue holds on to its answers because the assistant owes a reply (retry pending).</summary>
    public bool IsPaused { get; private set; }

    /// <summary>One answer is being sent right now.</summary>
    public bool IsSending => inFlight is not null;

    /// <summary>Anything sent or still to send. Undo stays disabled while this is true.</summary>
    public bool HasWork => inFlight is not null || waiting.Count > 0;

    public int WaitingCount => waiting.Count;

    /// <summary>The answers not yet stored, in the order they will be (or are being) sent.</summary>
    public IReadOnlyList<QueuedAnswer> Outstanding
    {
        get
        {
            var all = new List<QueuedAnswer>(waiting.Count + 1);
            if (inFlight is not null)
            {
                all.Add(inFlight);
            }

            all.AddRange(waiting);

            return all;
        }
    }

    public void Enqueue(QueuedAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if ((answer.Text is null) == (answer.QuickReplyKey is null))
        {
            throw new ArgumentException("An answer carries exactly one of text or quick reply key.", nameof(answer));
        }

        waiting.Enqueue(answer);
    }

    /// <summary>Takes the next answer to send; null while one is in flight, the queue is paused or nothing waits.</summary>
    public QueuedAnswer? StartNext()
    {
        if (inFlight is not null || IsPaused || !waiting.TryDequeue(out var next))
        {
            return null;
        }

        inFlight = next;

        return next;
    }

    /// <summary>The answer in flight is stored (or must not be sent again): forget it.</summary>
    public void Complete() => inFlight = null;

    /// <summary>Holds the remaining answers until <see cref="Resume"/>, for example while the assistant must retry.</summary>
    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    /// <summary>Drops everything and returns it in order, the one in flight first, so the user does not lose the text.</summary>
    public IReadOnlyList<QueuedAnswer> Abort()
    {
        var all = Outstanding;
        inFlight = null;
        waiting.Clear();
        IsPaused = false;

        return all;
    }
}
