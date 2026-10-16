using System.Text.RegularExpressions;

namespace RutaProducto.Infrastructure.Artifacts;

/// <summary>
/// Keeps invented figures out of a draft without hiding the gap. An item the model marked as an assumption may only
/// repeat numbers that the person's own texts (answers, description, approved Brief, attached context) already contain.
/// One that brings a new number or percentage is replaced by a fixed Spanish open question about its section, so the gap
/// is recorded and not silently dropped. The question has no text from the model and no figure; a section gets at most
/// one. Numbers are compared as digit tokens, so "99 %" matches "99%" and "1.000" matches "1,000". Pure: no clock, no
/// I/O, and nothing it returns for logging carries text.
/// </summary>
internal static partial class FigureGuard
{
    /// <summary>The fixed question for a section; it never repeats model text or a figure.</summary>
    public static string QuestionFor(string section) =>
        $"¿Qué valor o meta se espera para «{section}»? El borrador no debe fijar una cifra que nadie indicó.";

    /// <summary>
    /// The items with every figure-inventing assumption replaced by the section's question (the first one in place, the
    /// rest of that section removed). <paramref name="converted"/> is how many assumptions were replaced or removed.
    /// </summary>
    public static DraftItems Filter(DraftItems items, IReadOnlyList<DraftSlot> slots, IEnumerable<string?> inputs, out int converted)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        var knownPercent = false;

        foreach (var input in inputs.Where(i => !string.IsNullOrEmpty(i)))
        {
            known.UnionWith(Tokens(input!));
            knownPercent |= input!.Contains('%', StringComparison.Ordinal);
        }

        var count = 0;
        var asked = new HashSet<string>(StringComparer.Ordinal);
        var bySlot = new Dictionary<string, IReadOnlyList<DraftItem>>(StringComparer.Ordinal);

        foreach (var (slotId, list) in items.BySlot)
        {
            var section = slots.FirstOrDefault(s => s.Id == slotId)?.Section ?? slotId;
            var result = new List<DraftItem>();

            foreach (var item in list)
            {
                if (item.Source != DraftItemSource.Assumption || !Introduces(item.Text, known, knownPercent))
                {
                    result.Add(item);

                    continue;
                }

                count++;

                // One question per section, even when two slots share the section title.
                if (asked.Add(section))
                {
                    result.Add(new DraftItem(QuestionFor(section), DraftItemSource.Unknown));
                }
            }

            bySlot[slotId] = result;
        }

        converted = count;

        return items with { BySlot = bySlot };
    }

    /// <summary>True when the text has a number (or a percent sign with no number) that the known tokens do not cover.</summary>
    internal static bool Introduces(string text, ISet<string> known, bool knownPercent)
    {
        var tokens = Tokens(text);

        if (tokens.Count == 0)
        {
            return text.Contains('%', StringComparison.Ordinal) && !knownPercent;
        }

        return tokens.Any(token => !known.Contains(token));
    }

    // Digit runs with the thousands and decimal separators removed; identifiers such as "A-5" are not figures.
    internal static HashSet<string> Tokens(string text) =>
        [.. NumberRegex().Matches(IdRegex().Replace(text, " ")).Select(m => m.Value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal))];

    [GeneratedRegex(@"\b(?:FR|NFR|SM|UJ|A|Q|AD)\s?-\s?\d+\b", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"\d+(?:[.,]\d+)*", RegexOptions.None, 1000)]
    private static partial Regex NumberRegex();
}
