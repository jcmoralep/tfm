using System.Text.RegularExpressions;
using BmadPlatform.Domain.Common;

namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>
/// Writes the owner and the revisit condition of open questions into the Markdown. Only the questions listed are
/// rewritten (head, text and a canonical suffix); every other character of the document, line endings included, is
/// kept. A pure function of the text.
/// </summary>
public static partial class QuestionOwners
{
    public const int OwnerMaxLength = 60;

    public const int RevisitMaxLength = 80;

    private static readonly char[] Forbidden = ['(', ')', ';', '[', ']', '*', '|', '\\', '\r', '\n'];

    /// <summary>One rule for both fields: none of <c>( ) ; [ ] * | \</c> and no line break (null and empty are fine).</summary>
    public static bool IsAllowed(string? value) => value is null || value.IndexOfAny(Forbidden) < 0;

    /// <summary>The value as it is stored: edges trimmed and inner whitespace collapsed to single spaces.</summary>
    public static string Clean(string? value) => WhitespaceRegex().Replace((value ?? string.Empty).Trim(), " ");

    /// <summary>
    /// Every line whose question id is assigned (duplicated ids included) becomes <c>head + text + suffix</c>. An unknown
    /// id throws a <see cref="DomainException"/> and a value that is not allowed throws an <see cref="ArgumentException"/>
    /// (the validator rejects it first).
    /// </summary>
    public static string Apply(string markdown, IReadOnlyList<QuestionAssignment> assignments)
    {
        var lines = MarkdownLines.Split(markdown);
        var scanned = OpenQuestions.Scan(lines);
        var byId = new Dictionary<string, QuestionAssignment>(StringComparer.Ordinal);

        foreach (var assignment in assignments)
        {
            if (!IsAllowed(assignment.Owner) || !IsAllowed(assignment.Revisit))
            {
                throw new ArgumentException("The owner or the revisit text contains a character that is not allowed.", nameof(assignments));
            }

            if (!scanned.Any(s => s.Question.Id == assignment.Id))
            {
                throw new DomainException(ArtifactTexts.QuestionNotFound(assignment.Id));
            }

            byId[assignment.Id] = assignment;
        }

        // Even entries are the lines, odd ones their terminators, so the original line endings survive the rewrite.
        var parts = LineBreakRegex().Split(markdown);

        foreach (var item in scanned.Where(s => byId.ContainsKey(s.Question.Id)))
        {
            var assignment = byId[item.Question.Id];
            var owner = Clean(assignment.Owner) is { Length: > 0 } o ? o : OpenQuestions.OwnerPlaceholder;
            var revisit = Clean(assignment.Revisit) is { Length: > 0 } r ? r : OpenQuestions.RevisitPlaceholder;
            var text = item.Question.Text;

            parts[(item.Question.Line - 1) * 2] = item.Head + (text.Length > 0 ? " " + text : string.Empty) + OpenQuestions.Suffix(owner, revisit);
        }

        return string.Concat(parts);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(\r\n|\n|\r)")]
    private static partial Regex LineBreakRegex();
}
