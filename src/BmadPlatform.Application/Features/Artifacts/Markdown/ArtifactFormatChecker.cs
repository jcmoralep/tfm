using System.Text.RegularExpressions;
using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>
/// Reads a document and reports format warnings in Spanish: a missing required heading, a malformed or duplicated
/// identifier, a broken assumption marker or a marker and index that do not match. It is a pure function of the
/// text, never alters it and never blocks anything. Gaps and retired numbers are not warnings because that needs
/// history. Identifiers are read where a definition is expected (the start of a line, optionally as a list item
/// or in bold), so mentions inside a sentence are ignored. Fenced code is skipped.
/// </summary>
public static partial class ArtifactFormatChecker
{
    /// <summary>The most warnings returned; a badly broken document gets the first ones and a count of the rest.</summary>
    public const int MaxWarnings = 50;

    private const int ExcerptLength = 50;

    // Identifiers written without a hyphen ("FR3") are read as an attempt at the scheme only for these prefixes. For
    // the short ones (Q4, A4, SM2) the same text is an ordinary word, a quarter or a model name followed by prose.
    private static readonly string[] UnhyphenatedPrefixes = [ItemPrefix.NonFunctionalRequirement, ItemPrefix.FunctionalRequirement, ItemPrefix.UserJourney];

    public static IReadOnlyList<FormatWarning> Check(ArtifactType type, string markdown)
    {
        var lines = MarkdownLines.Split(markdown);
        var headings = MarkdownLines.ReadHeadings(lines);
        var warnings = new List<FormatWarning>();

        foreach (var section in ArtifactOutline.Sections(type))
        {
            if (!headings.Any(h => string.Equals(h.Title, section.Title, StringComparison.OrdinalIgnoreCase)))
            {
                warnings.Add(new FormatWarning(null, $"Falta la sección «{section.Title}»."));
            }
        }

        var index = MarkdownLines.FindIndexRange(headings, lines.Length);
        var fenced = MarkdownLines.FencedLines(lines);
        var definitions = new Dictionary<string, int>(StringComparer.Ordinal);
        var markers = new Dictionary<string, int>(StringComparer.Ordinal);
        var entries = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;

            if (fenced[i] || MarkdownLines.IsHeading(lines[i]))
            {
                continue;
            }

            var inIndex = index is { } range && number > range.First && number <= range.Last;

            if (!inIndex)
            {
                CheckMarkers(lines[i], number, markers, warnings);
            }

            CheckDefinition(lines[i], number, inIndex, definitions, entries, warnings);
        }

        if (index is not null)
        {
            warnings.AddRange(markers.Where(m => !entries.ContainsKey(m.Key))
                .Select(m => new FormatWarning(m.Value, $"{At(lines[m.Value - 1])}el supuesto {m.Key} no aparece en el Índice de suposiciones.")));
            warnings.AddRange(entries.Where(e => !markers.ContainsKey(e.Key))
                .Select(e => new FormatWarning(e.Value, $"{At(lines[e.Value - 1])}{e.Key} está en el Índice de suposiciones pero no tiene marcador {ArtifactOutline.AssumptionMarker(e.Key)} en el texto.")));
        }

        // Document order: whole-document findings first, then by line (the sort is stable).
        var ordered = warnings.OrderBy(w => w.Line ?? 0).ToList();

        if (ordered.Count <= MaxWarnings)
        {
            return ordered;
        }

        var rest = ordered.Count - MaxWarnings;

        return [.. ordered.Take(MaxWarnings), new FormatWarning(null, $"y {rest} avisos más.")];
    }

    // The read view has no line numbers, so a warning quotes the start of the offending line instead (nothing when the
    // line has no text left once the markup is removed).
    private static string At(string line)
    {
        var text = MarkupRegex().Replace(ListMarkerRegex().Replace(line, string.Empty), string.Empty).Trim();
        text = WhitespaceRegex().Replace(text, " ");

        if (text.Length > ExcerptLength)
        {
            var cut = char.IsHighSurrogate(text[ExcerptLength - 1]) ? ExcerptLength - 1 : ExcerptLength;
            text = text[..cut].TrimEnd() + "…";
        }

        return text.Length == 0 ? string.Empty : $"En «{text}»: ";
    }

    private static void CheckMarkers(string line, int number, Dictionary<string, int> markers, List<FormatWarning> warnings)
    {
        foreach (Match match in MarkerRegex().Matches(line))
        {
            var valid = ValidMarkerRegex().Match(match.Value);

            if (!valid.Success)
            {
                warnings.Add(new FormatWarning(number, $"{At(line)}el marcador [ASSUMPTION] no indica un identificador A-n válido."));
                continue;
            }

            var id = "A-" + valid.Groups["n"].Value;

            if (valid.Groups["n"].Value == "0")
            {
                warnings.Add(new FormatWarning(number, $"{At(line)}«{id}» no es válido: la numeración empieza en 1."));
            }
            else if (markers.ContainsKey(id))
            {
                warnings.Add(new FormatWarning(number, $"{At(line)}{id} ya está definido antes en el documento."));
            }
            else
            {
                markers[id] = number;
            }
        }
    }

    private static void CheckDefinition(
        string line,
        int number,
        bool inIndex,
        Dictionary<string, int> definitions,
        Dictionary<string, int> entries,
        List<FormatWarning> warnings)
    {
        var match = DefinitionRegex().Match(line);

        if (!match.Success)
        {
            return;
        }

        var token = match.Groups["tok"].Value;
        var prefix = match.Groups["p"].Value.ToUpperInvariant();
        var rest = match.Groups["rest"].Value;

        if (!ItemPrefix.All.Contains(prefix, StringComparer.Ordinal))
        {
            return;
        }

        var exact = match.Groups["p"].Value == prefix;
        var hasDigit = rest.Any(char.IsDigit);

        // Lowercase prefixes only count when a number follows, so ordinary words are never flagged. A single-letter
        // prefix followed by a longer word ("A-priori") is prose, not an identifier.
        if ((!exact && !hasDigit) || (prefix.Length == 1 && !hasDigit && rest.Count(char.IsLetter) > 1))
        {
            return;
        }

        // "Q4 Revenue" or "SM2 es un modelo" is prose: without a hyphen only the long prefixes count as an attempt.
        if (!rest.Contains('-') && !UnhyphenatedPrefixes.Contains(prefix, StringComparer.Ordinal))
        {
            return;
        }

        var valid = ValidIdRegex().Match(token);

        if (!valid.Success)
        {
            warnings.Add(new FormatWarning(number, $"{At(line)}«{token}» no sigue el formato {prefix}-n."));

            return;
        }

        if (valid.Groups["n"].Value == "0")
        {
            warnings.Add(new FormatWarning(number, $"{At(line)}«{token}» no es válido: la numeración empieza en 1."));

            return;
        }

        // Assumptions are defined by their inline marker; the index lines are references kept apart.
        if (prefix == ItemPrefix.Assumption && !inIndex)
        {
            return;
        }

        var target = prefix == ItemPrefix.Assumption ? entries : definitions;

        if (target.ContainsKey(token))
        {
            warnings.Add(new FormatWarning(number, $"{At(line)}{token} ya está definido antes en el documento."));
        }
        else
        {
            target[token] = number;
        }
    }

    [GeneratedRegex(@"^\s*[-+*]\s+")]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"[*_`#]")]
    private static partial Regex MarkupRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\s*(?:[-*+]\s+)?(?:\*\*)?(?<tok>(?<p>[A-Za-z]{1,3})(?<rest>\s?-\s?[A-Za-z0-9]*|[0-9]+))(?=\*\*|\s|:|\.|\)|$)")]
    private static partial Regex DefinitionRegex();

    [GeneratedRegex(@"^(?<p>NFR|FR|UJ|SM|A|Q)-(?<n>0|[1-9][0-9]*)$")]
    private static partial Regex ValidIdRegex();

    [GeneratedRegex(@"\[ASSUMPTION[^\]]*\]?")]
    private static partial Regex MarkerRegex();

    [GeneratedRegex(@"^\[ASSUMPTION: A-(?<n>0|[1-9][0-9]*)\]$")]
    private static partial Regex ValidMarkerRegex();
}
