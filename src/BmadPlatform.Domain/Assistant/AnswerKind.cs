namespace BmadPlatform.Domain.Assistant;

/// <summary>How the user answered a question. <see cref="Unknown"/> is "No sé", kept apart so open questions can be listed later.</summary>
public enum AnswerKind
{
    FreeText,
    QuickReply,
    Unknown,
}
