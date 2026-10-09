namespace BmadPlatform.Web.Components.Layout;

/// <summary>Decides which navigation link matches the current page.</summary>
public static class NavSection
{
    /// <summary>
    /// True when <paramref name="relativePath"/> (as returned by <c>ToBaseRelativePath</c>, query string allowed)
    /// is the section itself or lies below it. "iniciativas-x" is not part of "iniciativas".
    /// </summary>
    public static bool IsActive(string relativePath, string section)
    {
        var path = relativePath.AsSpan();
        var end = path.IndexOfAny('?', '#');
        if (end >= 0)
        {
            path = path[..end];
        }

        path = path.Trim('/');
        var target = section.AsSpan().Trim('/');

        return path.Equals(target, StringComparison.OrdinalIgnoreCase)
            || (path.Length > target.Length
                && path.StartsWith(target, StringComparison.OrdinalIgnoreCase)
                && path[target.Length] == '/');
    }
}
