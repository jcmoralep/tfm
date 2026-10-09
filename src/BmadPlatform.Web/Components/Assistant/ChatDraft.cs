namespace BmadPlatform.Web.Components.Assistant;

public enum DraftSubmission
{
    /// <summary>The text was queued and the composer cleared.</summary>
    Queued,

    /// <summary>Nothing but whitespace: ignored, which also makes a double submit store the answer once.</summary>
    Empty,

    /// <summary>More than 2000 characters after trimming: kept in the composer for the user to shorten.</summary>
    TooLong,
}

/// <summary>The text in the composer and the rules for turning it into a queued answer.</summary>
public sealed class ChatDraft
{
    public const int MaxLength = 2000;

    /// <summary>The limit as shown in the counter; the thousands separator is written literally to stay culture independent.</summary>
    public const string MaxLengthText = "2.000";

    public string Text { get; set; } = "";

    public string Counter => $"{Text.Length} / {MaxLengthText}";

    public bool HasContent => !string.IsNullOrWhiteSpace(Text);

    public bool IsTooLong => Text.Trim().Length > MaxLength;

    /// <summary>
    /// Trims the text and queues it. The composer is cleared before anything is awaited, so a second click on
    /// "Enviar" finds it empty and queues nothing.
    /// </summary>
    public DraftSubmission TrySubmit(SendQueue queue)
    {
        var trimmed = Text.Trim();

        if (trimmed.Length == 0)
        {
            return DraftSubmission.Empty;
        }

        if (trimmed.Length > MaxLength)
        {
            return DraftSubmission.TooLong;
        }

        queue.Enqueue(QueuedAnswer.ForText(trimmed));
        Text = "";

        return DraftSubmission.Queued;
    }

    /// <summary>Puts text that was not stored back in the composer, ahead of whatever was typed meanwhile.</summary>
    public void Restore(IEnumerable<QueuedAnswer> answers)
    {
        var texts = answers
            .Select(answer => answer.Text)
            .Append(string.IsNullOrWhiteSpace(Text) ? null : Text.Trim())
            .Where(text => !string.IsNullOrEmpty(text));

        Text = string.Join("\n\n", texts);
    }
}
