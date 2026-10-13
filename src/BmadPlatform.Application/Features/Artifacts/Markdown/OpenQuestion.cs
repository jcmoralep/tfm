namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>
/// One open question as written in the Markdown: <c>- **Q-1**: text (Responsable: owner; Revisar: revisit)</c>.
/// <paramref name="Owner"/> and <paramref name="Revisit"/> are null when the line has no readable suffix (or no
/// <c>Revisar</c> part), trimmed otherwise.
/// </summary>
public sealed record OpenQuestion(string Id, int Line, string Text, string? Owner, string? Revisit)
{
    /// <summary>Both the owner and the revisit condition are filled in and neither is the default placeholder.</summary>
    public bool IsAssigned => Filled(Owner, OpenQuestions.OwnerPlaceholder) && Filled(Revisit, OpenQuestions.RevisitPlaceholder);

    private static bool Filled(string? value, string placeholder) =>
        !string.IsNullOrWhiteSpace(value) && !string.Equals(value.Trim(), placeholder, StringComparison.OrdinalIgnoreCase);
}
