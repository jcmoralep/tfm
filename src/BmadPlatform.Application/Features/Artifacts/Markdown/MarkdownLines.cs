using System.Text.RegularExpressions;

namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>
/// How the document readers see the text: the lines, the fenced code to skip, the headings and the assumption index
/// range. Shared by the format checker and the open-question reader so both recognise the same lines.
/// </summary>
internal static partial class MarkdownLines
{
    public static string[] Split(string? markdown) =>
        (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    public static bool IsHeading(string line) => HeadingRegex().IsMatch(line);

    public static bool[] FencedLines(string[] lines)
    {
        var fenced = new bool[lines.Length];
        var open = false;

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced[i] = true;
                open = !open;
            }
            else
            {
                fenced[i] = open;
            }
        }

        return fenced;
    }

    public static List<(int Line, int Level, string Title)> ReadHeadings(string[] lines)
    {
        var fenced = FencedLines(lines);
        var headings = new List<(int, int, string)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var match = HeadingRegex().Match(lines[i]);

            if (fenced[i] || !match.Success)
            {
                continue;
            }

            var title = NumberPrefixRegex().Replace(match.Groups["title"].Value.Trim().TrimEnd('#').Trim(), string.Empty);
            headings.Add((i + 1, match.Groups["hashes"].Length, title));
        }

        return headings;
    }

    public static (int First, int Last)? FindIndexRange(List<(int Line, int Level, string Title)> headings, int lineCount) =>
        FindSectionRange(headings, ArtifactOutline.AssumptionIndexTitle, lineCount);

    // A section runs from its heading to the next heading of the same or a higher level.
    public static (int First, int Last)? FindSectionRange(List<(int Line, int Level, string Title)> headings, string title, int lineCount)
    {
        var position = headings.FindIndex(h => string.Equals(h.Title, title, StringComparison.OrdinalIgnoreCase));

        if (position < 0)
        {
            return null;
        }

        var start = headings[position];
        var next = headings.Skip(position + 1).FirstOrDefault(h => h.Level <= start.Level);

        return (start.Line, next == default ? lineCount : next.Line - 1);
    }

    [GeneratedRegex(@"^\s{0,3}(?<hashes>#{1,6})\s+(?<title>.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\d+(\.\d+)*\.?\s+")]
    private static partial Regex NumberPrefixRegex();
}
