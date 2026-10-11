namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>
/// Hands out identifiers per prefix, starting at 1 and without gaps. A drafter creates a new numberer for every
/// document, so regenerating always numbers from scratch.
/// </summary>
public sealed class ItemNumberer
{
    private readonly Dictionary<string, int> _last = new(StringComparer.Ordinal);

    public string Next(string prefix)
    {
        var number = _last.GetValueOrDefault(prefix) + 1;
        _last[prefix] = number;

        return $"{prefix}-{number}";
    }

    /// <summary>How many identifiers of the prefix were handed out so far.</summary>
    public int Count(string prefix) => _last.GetValueOrDefault(prefix);
}
