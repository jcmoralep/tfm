using System.Text.RegularExpressions;
using RutaProducto.Application.Features.Artifacts.Markdown;

namespace RutaProducto.Infrastructure.Artifacts;

/// <summary>
/// What the approved PRD defines: its functional and non-functional requirements in document order. Layouts cite only
/// identifiers this PRD defines, so a model cannot bind a decision or a story to a requirement that does not exist.
/// </summary>
internal sealed partial class PrdFacts
{
    private readonly Dictionary<string, int> order = new(StringComparer.Ordinal);

    public PrdFacts(string? prdMarkdown)
    {
        All = RequirementItems.Parse(prdMarkdown);
        Functional = [.. All.Where(item => item.Id.StartsWith("FR-", StringComparison.Ordinal))];
        NonFunctional = [.. All.Where(item => item.Id.StartsWith("NFR-", StringComparison.Ordinal))];

        for (var i = 0; i < All.Count; i++)
        {
            order[All[i].Id] = i;
        }
    }

    /// <summary>Every requirement, in the order the PRD writes them.</summary>
    public IReadOnlyList<RequirementItem> All { get; }

    public IReadOnlyList<RequirementItem> Functional { get; }

    public IReadOnlyList<RequirementItem> NonFunctional { get; }

    public bool Knows(string? id) => id is not null && order.ContainsKey(id);

    /// <summary>The position of a requirement in the PRD, to sort citations in PRD order.</summary>
    public int PositionOf(string id) => order.GetValueOrDefault(id, int.MaxValue);

    /// <summary>
    /// The canonical form of an identifier a model wrote: <c>fr3</c>, <c>FR 3</c> and <c>FR-3</c> give <c>FR-3</c>. Null
    /// when the text is not a functional or non-functional requirement identifier.
    /// </summary>
    public static string? CanonicalId(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        try
        {
            var match = IdRegex().Match(raw);

            return match.Success ? $"{match.Groups["p"].Value.ToUpperInvariant()}-{match.Groups["n"].Value}" : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^\s*(?<p>NFR|FR)\s?-?\s?(?<n>[1-9][0-9]{0,5})\s*$", RegexOptions.IgnoreCase, 250)]
    private static partial Regex IdRegex();
}
