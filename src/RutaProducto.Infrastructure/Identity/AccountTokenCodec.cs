using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace RutaProducto.Infrastructure.Identity;

/// <summary>
/// Identity tokens are Base64 text; links carry them as Base64Url so no character needs escaping.
/// Decoding never throws: a malformed code is just null.
/// </summary>
internal static class AccountTokenCodec
{
    public const int MaxCodeLength = 2048;

    public static string Encode(string identityToken) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(identityToken));

    public static string? Decode(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > MaxCodeLength)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
