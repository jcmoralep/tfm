using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using ArtifactQuestions = BmadPlatform.Application.Features.Artifacts.Markdown.OpenQuestions;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// What the PRD takes from the approved Brief besides the model's items. A PRD item the model calls an answer but that
/// only repeats an assumption of the Brief is downgraded to an assumption (the Brief never proved it), and the open
/// questions of the Brief that the model did not restate are carried over, so neither is lost or promoted to a fact.
/// The match is a HEURISTIC on normalized text (no case, accents or punctuation; equal, or one contains the other as
/// whole words when it has at least four words): a reworded assumption can escape it, and the prompt is the first guard.
/// </summary>
internal static partial class BriefFacts
{
    // So a Brief full of questions cannot outgrow the content cap of the PRD.
    private const int MaxCarriedQuestions = 10;

    private const int MinWordsForContainment = 4;

    public static DraftItems Apply(DraftItems items, string briefMarkdown)
    {
        var assumptions = Assumptions(briefMarkdown).Select(Normalize).Where(text => text.Length > 0).ToList();
        var bySlot = new Dictionary<string, IReadOnlyList<DraftItem>>(StringComparer.Ordinal);
        var unknowns = new List<string>();

        foreach (var (slot, slotItems) in items.BySlot)
        {
            bySlot[slot] =
            [
                .. slotItems.Select(item =>
                {
                    var normalized = Normalize(DraftComposer.ModelText(item.Text));

                    if (item.Source == DraftItemSource.Unknown)
                    {
                        unknowns.Add(normalized);
                    }

                    return item.Source == DraftItemSource.Answer && assumptions.Any(known => Matches(normalized, known))
                        ? item with { Source = DraftItemSource.Assumption }
                        : item;
                }),
            ];
        }

        var carried = ArtifactQuestions.Parse(briefMarkdown)
            .Select(question => question.Text)
            .Where(text => Normalize(text) is { Length: > 0 } normalized && !unknowns.Any(unknown => Matches(unknown, normalized)))
            .DistinctBy(Normalize)
            .Take(MaxCarriedQuestions)
            .ToList();

        return new DraftItems(bySlot, carried);
    }

    /// <summary>The text of every assumption defined in the Índice de suposiciones of a Brief.</summary>
    public static IEnumerable<string> Assumptions(string briefMarkdown) =>
        IndexLineRegex().Matches(briefMarkdown).Select(match => match.Groups["text"].Value.Trim());

    /// <summary>Lowercase letters and digits only, accents removed, single spaces: the form two texts are compared in.</summary>
    public static string Normalize(string? text)
    {
        var builder = new StringBuilder();

        foreach (var c in (text ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool Matches(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);

        return shorter.Count(c => c == ' ') + 1 >= MinWordsForContainment && $" {longer} ".Contains($" {shorter} ", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^\s*-\s+\*\*A-\d+\*\*\s*:?\s*(?<text>\S.*)$", RegexOptions.Multiline, 1000)]
    private static partial Regex IndexLineRegex();
}
