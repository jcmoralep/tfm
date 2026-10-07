namespace BmadPlatform.Web.Authentication;

/// <summary>
/// Guards post-login redirects against open-redirect attacks: only application-relative paths are allowed.
/// </summary>
internal static class LocalRedirect
{
    public const string Fallback = "/";

    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return Fallback;
        }

        var isLocal = returnUrl[0] == '/'
            && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
            && !returnUrl.Any(char.IsControl);

        return isLocal ? returnUrl : Fallback;
    }
}
