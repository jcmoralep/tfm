using System.Globalization;
using System.Text.RegularExpressions;
using RutaProducto.Domain.Common;

namespace RutaProducto.Application.Features.Artifacts.Markdown;

/// <summary>
/// Writes the owner and the revisit condition of open questions into the Markdown. Only the questions listed are
/// rewritten (head, text and a canonical suffix); every other character of the document, line endings included, is
/// kept. A pure function of the text.
/// </summary>
public static partial class QuestionOwners
{
    public const int OwnerMaxLength = 60;

    public const int RevisitMaxLength = 80;

    private static readonly char[] Forbidden = ['(', ')', ';', '[', ']', '*', '|', '\\', '<', '>', '`'];

    /// <summary>
    /// One rule for both fields: none of <c>( ) ; [ ] * | \ &lt; &gt; `</c>, no control character (line breaks included)
    /// and no invisible format character such as a bidi override (null and empty are fine).
    /// </summary>
    public static bool IsAllowed(string? value) =>
        value is null || (value.IndexOfAny(Forbidden) < 0 && value.All(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format));

    /// <summary>The value as it is stored: edges trimmed and inner whitespace collapsed to single spaces.</summary>
    public static string Clean(string? value) => WhitespaceRegex().Replace((value ?? string.Empty).Trim(), " ");

    /// <summary>
    /// Every line whose question id is assigned (duplicated ids included) becomes <c>head + text + suffix</c>. An unknown
    /// id throws a <see cref="DomainException"/> and a value that is not allowed or too long throws an
    /// <see cref="ArgumentException"/> (the validator rejects it first).
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

            if (Clean(assignment.Owner).Length > OwnerMaxLength || Clean(assignment.Revisit).Length > RevisitMaxLength)
            {
                throw new ArgumentException("The owner or the revisit text is longer than allowed.", nameof(assignments));
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

            // Already carrying these values: keep the hand-written line as it is, so the no-op also holds for it.
            if (item.Question.Owner == owner && item.Question.Revisit == revisit)
            {
                continue;
            }

            parts[(item.Question.Line - 1) * 2] = item.Head + (text.Length > 0 ? " " + text : string.Empty) + OpenQuestions.Suffix(owner, revisit);
        }

        return string.Concat(parts);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(\r\n|\n|\r)")]
    private static partial Regex LineBreakRegex();
}
