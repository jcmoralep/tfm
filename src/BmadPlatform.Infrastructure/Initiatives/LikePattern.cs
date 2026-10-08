namespace BmadPlatform.Infrastructure.Initiatives;

/// <summary>Builds LIKE patterns from user text so that wildcard characters match literally.</summary>
internal static class LikePattern
{
    public const string EscapeCharacter = "\\";

    /// <summary>Pattern that matches any value containing <paramref name="term"/>, to be used with <see cref="EscapeCharacter"/>.</summary>
    public static string Contains(string term) => $"%{Escape(term)}%";

    private static string Escape(string term) => term
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
