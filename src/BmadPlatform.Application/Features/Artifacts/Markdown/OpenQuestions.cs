using System.Text.RegularExpressions;

namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>
/// Reads the open questions of a document. A question is recognised like the format checker recognises a definition
/// (line start, optional list marker and bold, a valid hyphenated number from 1) outside fenced code, headings and the
/// assumption index. The owner and the revisit condition live in a suffix at the end of the line, so the Markdown
/// stays the only source of truth. A pure function of the text.
/// </summary>
public static partial class OpenQuestions
{
    public const string OwnerPlaceholder = "por asignar";

    public const string RevisitPlaceholder = "por definir";

    /// <summary>The one grammar of a question id, shared by the reader and the assign command (up to 6 digits).</summary>
    public const string IdPattern = "Q-[1-9][0-9]{0,5}";

    // Safety net: the patterns are linear, but they run on text people edit.
    private const int MatchTimeoutMs = 250;

    public static bool IsValidId(string? id) => id is not null && IdRegex().IsMatch(id);

    /// <summary>The suffix every generated question carries until someone assigns it.</summary>
    public static string DefaultSuffix { get; } = Suffix(OwnerPlaceholder, RevisitPlaceholder);

    /// <summary>
    /// The suffix of the questions of the Architecture and Epics drafts: the development team owns them and answers
    /// them before building, so they are already assigned and never block approval.
    /// </summary>
    public static string DevelopmentSuffix { get; } = Suffix("Equipo de desarrollo", "antes de construir");

    public static string Suffix(string owner, string revisit) => $" (Responsable: {owner}; Revisar: {revisit})";

    /// <summary>Every open question in document order; a duplicated id appears once per line.</summary>
    public static IReadOnlyList<OpenQuestion> Parse(string markdown) =>
        [.. Scan(MarkdownLines.Split(markdown)).Select(scanned => scanned.Question)];

    /// <summary>The questions with the definition prefix as written, so a rewrite can keep it.</summary>
    internal static List<ScannedQuestion> Scan(string[] lines)
    {
        var fenced = MarkdownLines.FencedLines(lines);
        var index = MarkdownLines.FindIndexRange(MarkdownLines.ReadHeadings(lines), lines.Length);
        var found = new List<ScannedQuestion>();

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;

            if (fenced[i] || MarkdownLines.IsHeading(lines[i]) || (index is { } range && number > range.First && number <= range.Last))
            {
                continue;
            }

            if (Read(lines[i], number) is { } scanned)
            {
                found.Add(scanned);
            }
        }

        return found;
    }

    /// <summary>The ids of the questions still to assign, without repeats, in document order.</summary>
    public static IReadOnlyList<string> Unassigned(string markdown) =>
        [.. Parse(markdown).Where(q => !q.IsAssigned).Select(q => q.Id).Distinct(StringComparer.Ordinal)];

    private static ScannedQuestion? Read(string line, int number)
    {
        Match definition;
        Match suffix;
        string rest;

        try
        {
            definition = DefinitionRegex().Match(line);

            if (!definition.Success)
            {
                return null;
            }

            rest = line[definition.Length..].TrimEnd();
            suffix = SuffixRegex().Match(rest);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        if (!suffix.Success)
        {
            return new ScannedQuestion(new OpenQuestion(definition.Groups["id"].Value, number, rest.Trim(), null, null), definition.Value);
        }

        var revisit = suffix.Groups["revisit"];

        return new ScannedQuestion(
            new OpenQuestion(
                definition.Groups["id"].Value,
                number,
                rest[..suffix.Index].Trim(),
                suffix.Groups["owner"].Value.Trim(),
                revisit.Success ? revisit.Value.Trim() : null),
            definition.Value);
    }

    [GeneratedRegex(@"^\s*(?:[-*+]\s+)?(?:\*\*)?(?<id>" + IdPattern + @")(?=\*\*|\s|:|\.|\)|$)(?:\*\*)?[:.)]?(?:\*\*)?", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex DefinitionRegex();

    // No lazy quantifiers and no whitespace run beside a class that also matches whitespace: the values are trimmed in code.
    [GeneratedRegex(@"\(\s*Responsable\s*:(?<owner>[^();]*)(?:;\s*Revisar\s*:(?<revisit>[^();]*))?\)\z", RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex SuffixRegex();

    [GeneratedRegex(@"^" + IdPattern + @"\z", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex IdRegex();
}

/// <summary>An open question with the text that precedes its body on the line (indent, marker, bold id, colon).</summary>
internal sealed record ScannedQuestion(OpenQuestion Question, string Head);
