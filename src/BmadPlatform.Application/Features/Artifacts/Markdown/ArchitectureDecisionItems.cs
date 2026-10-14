using System.Text.RegularExpressions;

namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>One architecture decision of a document: its identifier (<c>AD-2</c>) and the title after the dash.</summary>
public sealed record ArchitectureDecisionItem(string Id, string Title);

/// <summary>
/// Reads the decisions of an Architecture draft from their headings, <c>### AD-n — title</c> (a valid hyphenated number
/// from 1). Fenced code and the assumption index are skipped, and the first occurrence of an identifier wins.
/// </summary>
public static partial class ArchitectureDecisionItems
{
    private const int MatchTimeoutMs = 250;

    public static IReadOnlyList<ArchitectureDecisionItem> Parse(string? markdown)
    {
        var lines = MarkdownLines.Split(markdown);
        var fenced = MarkdownLines.FencedLines(lines);
        var index = MarkdownLines.FindIndexRange(MarkdownLines.ReadHeadings(lines), lines.Length);
        var found = new List<ArchitectureDecisionItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;

            if (fenced[i] || (index is { } range && number > range.First && number <= range.Last))
            {
                continue;
            }

            try
            {
                var match = HeadingRegex().Match(lines[i]);

                if (match.Success && seen.Add(match.Groups["id"].Value))
                {
                    found.Add(new ArchitectureDecisionItem(match.Groups["id"].Value, match.Groups["title"].Value.Trim()));
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A line the pattern cannot read in time is not a decision.
            }
        }

        return found;
    }

    [GeneratedRegex(@"^\s{0,3}#{2,6}[ \t]+(?<id>AD-[1-9][0-9]{0,5})[ \t]*[—–-][ \t]*(?<title>.*)$", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex HeadingRegex();
}
