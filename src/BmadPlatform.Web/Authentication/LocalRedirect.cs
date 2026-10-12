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

    /// <summary>Builds a link to another sign-in page that keeps a safe return address, if there is one.</summary>
    public static string WithReturnUrl(string path, string? returnUrl)
    {
        var safe = Sanitize(returnUrl);
        return safe == Fallback ? path : $"{path}?returnUrl={Uri.EscapeDataString(safe)}";
    }
}
