using Microsoft.Extensions.Configuration;

namespace RutaProducto.Infrastructure.Captcha;

/// <summary>Which verifier backs the captcha seam (<c>Captcha:Provider</c>).</summary>
public enum CaptchaProvider
{
    None,
    Turnstile,
}

/// <summary>
/// Settings under <c>Captcha:</c> and <c>Turnstile:</c>, validated at startup. An empty provider means
/// <see cref="CaptchaProvider.None"/>. Failure messages name the option or the environment variable and never echo a value.
/// </summary>
public sealed class CaptchaOptions
{
    public const string ProviderKey = "Captcha:Provider";

    public const string SiteKeyKey = "Turnstile:SiteKey";

    public const string SecretKeyKey = "Turnstile:SecretKey";

    public CaptchaProvider Provider { get; private set; } = CaptchaProvider.None;

    public string? SiteKey { get; private set; }

    public string? SecretKey { get; private set; }

    public static CaptchaOptions BindAndValidate(IConfiguration configuration)
    {
        var options = new CaptchaOptions { Provider = ReadProvider(configuration[ProviderKey]) };

        if (options.Provider == CaptchaProvider.None)
        {
            return options;
        }

        var siteKey = configuration[SiteKeyKey];
        var secretKey = configuration[SecretKeyKey];

        if (string.IsNullOrWhiteSpace(siteKey))
        {
            throw new InvalidOperationException(
                "Captcha:Provider is Turnstile but the site key is not configured. Set Turnstile__SiteKey.");
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Captcha:Provider is Turnstile but the secret key is not configured. " +
                "Set Turnstile__SecretKey (or the user-secret Turnstile:SecretKey).");
        }

        options.SiteKey = siteKey.Trim();
        options.SecretKey = secretKey.Trim();

        return options;
    }

    private static CaptchaProvider ReadProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CaptchaProvider.None;
        }

        if (Enum.TryParse<CaptchaProvider>(value.Trim(), ignoreCase: true, out var provider) && Enum.IsDefined(provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"'{ProviderKey}' has an unknown value. Valid values are: None, Turnstile.");
    }
}
