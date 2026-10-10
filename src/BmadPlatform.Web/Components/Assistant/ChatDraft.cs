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

    /// <summary>
    /// Changes only when the code replaces the text (clear, restore), never while the person types. The box uses it
    /// to rebuild itself, so a render never writes an outdated value over what is being typed.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>Replaces the text from code, as opposed to typing.</summary>
    public void Replace(string text)
    {
        Text = text;
        Revision++;
    }

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
        Replace("");

        return DraftSubmission.Queued;
    }

    /// <summary>
    /// Puts text that was not stored back in the composer, ahead of whatever was typed meanwhile. A chosen button
    /// has no text to put back, so its label is reported instead of being lost silently, and the joined text is
    /// kept even when it passes the limit: the counter turns red and the person shortens it.
    /// </summary>
    public DraftRestoration Restore(IEnumerable<QueuedAnswer> answers)
    {
        var all = answers.ToList();

        var texts = all
            .Select(answer => answer.Text)
            .Append(string.IsNullOrWhiteSpace(Text) ? null : Text.Trim())
            .Where(text => !string.IsNullOrEmpty(text));

        Replace(string.Join("\n\n", texts));

        return new DraftRestoration([.. all.Where(answer => answer.Text is null).Select(answer => answer.Display)], IsTooLong);
    }
}

/// <summary>What <see cref="ChatDraft.Restore"/> could not give back as text.</summary>
/// <param name="DroppedChoices">Labels of the quick replies that were not sent; the person must choose them again.</param>
/// <param name="TooLong">The restored text is over the limit and cannot be sent as it is.</param>
public sealed record DraftRestoration(IReadOnlyList<string> DroppedChoices, bool TooLong);
