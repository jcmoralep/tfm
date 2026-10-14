namespace BmadPlatform.Infrastructure.Gemini;

public static class PromptText
{
    /// <summary>
    /// Replaces angle brackets in user-authored text so it cannot close or open a delimiter tag in the prompt
    /// (for example <c>&lt;/transcript&gt;</c>). Applied to every user string before it enters the user content.
    /// </summary>
    public static string Neutralize(string? userText) =>
        (userText ?? string.Empty).Replace('<', '‹').Replace('>', '›');
}
