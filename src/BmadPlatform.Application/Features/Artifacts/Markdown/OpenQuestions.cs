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

    /// <summary>The suffix every generated question carries until someone assigns it.</summary>
    public static string DefaultSuffix { get; } = Suffix(OwnerPlaceholder, RevisitPlaceholder);

    public static string Suffix(string owner, string revisit) => $" (Responsable: {owner}; Revisar: {revisit})";

    /// <summary>Every open question in document order; a duplicated id appears once per line.</summary>
    public static IReadOnlyList<OpenQuestion> Parse(string markdown)
    {
        var lines = MarkdownLines.Split(markdown);
        var fenced = MarkdownLines.FencedLines(lines);
        var index = MarkdownLines.FindIndexRange(MarkdownLines.ReadHeadings(lines), lines.Length);
        var found = new List<OpenQuestion>();

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;

            if (fenced[i] || MarkdownLines.IsHeading(lines[i]) || (index is { } range && number > range.First && number <= range.Last))
            {
                continue;
            }

            if (Read(lines[i], number) is { } question)
            {
                found.Add(question);
            }
        }

        return found;
    }

    /// <summary>The ids of the questions still to assign, without repeats, in document order.</summary>
    public static IReadOnlyList<string> Unassigned(string markdown) =>
        [.. Parse(markdown).Where(q => !q.IsAssigned).Select(q => q.Id).Distinct(StringComparer.Ordinal)];

    private static OpenQuestion? Read(string line, int number)
    {
        var definition = DefinitionRegex().Match(line);

        if (!definition.Success)
        {
            return null;
        }

        var rest = line[definition.Length..].TrimEnd();
        var suffix = SuffixRegex().Match(rest);

        if (!suffix.Success)
        {
            return new OpenQuestion(definition.Groups["id"].Value, number, rest.Trim(), null, null);
        }

        var revisit = suffix.Groups["revisit"];

        return new OpenQuestion(
            definition.Groups["id"].Value,
            number,
            rest[..suffix.Index].Trim(),
            suffix.Groups["owner"].Value.Trim(),
            revisit.Success ? revisit.Value.Trim() : null);
    }

    [GeneratedRegex(@"^\s*(?:[-*+]\s+)?(?:\*\*)?(?<id>Q-[1-9][0-9]*)(?=\*\*|\s|:|\.|\)|$)(?:\*\*)?[:.)]?(?:\*\*)?")]
    private static partial Regex DefinitionRegex();

    [GeneratedRegex(@"\(\s*Responsable\s*:\s*(?<owner>[^();]*?)\s*(?:;\s*Revisar\s*:\s*(?<revisit>[^();]*?)\s*)?\)$", RegexOptions.IgnoreCase)]
    private static partial Regex SuffixRegex();
}
