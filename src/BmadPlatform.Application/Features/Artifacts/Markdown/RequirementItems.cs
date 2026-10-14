using System.Text.RegularExpressions;

namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>One requirement of a document: its identifier (<c>FR-3</c>) and the text after it.</summary>
public sealed record RequirementItem(string Id, string Text);

/// <summary>
/// Reads the functional and non-functional requirements of a PRD: the list lines <c>- **FR-n**: text</c> and
/// <c>- **NFR-n**: text</c> (bold optional, a valid hyphenated number from 1). Fenced code, headings and the assumption
/// index are skipped, and the first occurrence of an identifier wins. A pure function of the text.
/// </summary>
public static partial class RequirementItems
{
    private const int MatchTimeoutMs = 250;

    public static IReadOnlyList<RequirementItem> Parse(string? markdown)
    {
        var lines = MarkdownLines.Split(markdown);
        var fenced = MarkdownLines.FencedLines(lines);
        var index = MarkdownLines.FindIndexRange(MarkdownLines.ReadHeadings(lines), lines.Length);
        var found = new List<RequirementItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;

            if (fenced[i] || MarkdownLines.IsHeading(lines[i]) || (index is { } range && number > range.First && number <= range.Last))
            {
                continue;
            }

            try
            {
                var match = ItemRegex().Match(lines[i]);

                if (match.Success && seen.Add(match.Groups["id"].Value))
                {
                    found.Add(new RequirementItem(match.Groups["id"].Value, match.Groups["text"].Value.Trim()));
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A line the pattern cannot read in time is not an item.
            }
        }

        return found;
    }

    [GeneratedRegex(@"^\s*[-*+]\s+(?:\*\*)?(?<id>(?:NFR|FR)-[1-9][0-9]{0,5})(?=\*\*|\s|:|\.|\)|$)(?:\*\*)?[:.)]?(?:\*\*)?(?<text>.*)$", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex ItemRegex();
}
