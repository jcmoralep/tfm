using System.Text;

namespace BmadPlatform.Domain.Context;

/// <summary>
/// Normalizes a browser-supplied file name once, at creation. The result is safe to show, to store and to place in
/// a download header: no path parts, no control or reserved characters, never empty, at most 255 characters.
/// </summary>
public static class AttachmentFileName
{
    public const int MaxLength = 255;

    public const string Fallback = "archivo";

    private const string ReservedCharacters = ":*?\"<>|";

    public static string Normalize(string? fileName)
    {
        var name = fileName ?? string.Empty;
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];

        var builder = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            builder.Append(char.IsControl(character) || ReservedCharacters.Contains(character) ? '_' : character);
        }

        var cleaned = Trim(builder.ToString());

        if (cleaned.Length == 0)
        {
            return Fallback;
        }

        if (cleaned.Length > MaxLength)
        {
            var extension = ExtensionOf(cleaned);
            var keep = extension.Length < MaxLength ? extension : string.Empty;
            cleaned = Trim(cleaned[..(MaxLength - keep.Length)]) + keep;
        }

        return cleaned.Length == 0 ? Fallback : cleaned;
    }

    /// <summary>The text after the last dot, with the dot, or an empty string when the name has no extension.</summary>
    public static string ExtensionOf(string name)
    {
        var index = name.LastIndexOf('.');

        return index < 0 ? string.Empty : name[index..];
    }

    private static string Trim(string value) => value.Trim(' ', '.');
}
