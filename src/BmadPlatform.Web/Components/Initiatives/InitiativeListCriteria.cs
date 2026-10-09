using BmadPlatform.Domain.Initiatives;
using Microsoft.AspNetCore.WebUtilities;

namespace BmadPlatform.Web.Components.Initiatives;

/// <summary>
/// Search and filters of the list page, kept in the query string so a refresh or a shared link keeps them.
/// Unknown or malformed values are ignored instead of failing the page.
/// </summary>
public sealed record InitiativeListCriteria(string? Search, InitiativeStatus? Status, InitiativeDepth? Depth)
{
    public const string ListPath = "/iniciativas";

    /// <summary>Statuses offered by the filter, in lifecycle order.</summary>
    public static IReadOnlyList<InitiativeStatus> StatusOptions { get; } =
        [InitiativeStatus.Draft, InitiativeStatus.Clarifying, InitiativeStatus.Planning, InitiativeStatus.ReadyToBuild];

    /// <summary>Depth levels offered by the filter. "Pendiente de sugerencia" is deliberately not an option.</summary>
    public static IReadOnlyList<InitiativeDepth> DepthOptions { get; } =
        [InitiativeDepth.Small, InitiativeDepth.Standard, InitiativeDepth.Large];

    /// <summary>True when nothing narrows the list (a whitespace-only search counts as no search).</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Search) && Status is null && Depth is null;

    public static InitiativeListCriteria FromQuery(string? search, string? status, string? depth) =>
        new(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), ParseEnum<InitiativeStatus>(status), ParseEnum<InitiativeDepth>(depth));

    /// <summary>The list address carrying only the active criteria.</summary>
    public string ToRelativeUri()
    {
        var query = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query["q"] = Search.Trim();
        }

        if (Status is { } status)
        {
            query["estado"] = status.ToString();
        }

        if (Depth is { } depth)
        {
            query["nivel"] = depth.ToString();
        }

        return QueryHelpers.AddQueryString(ListPath, query);
    }

    private static T? ParseEnum<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
}
