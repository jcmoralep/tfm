using RutaProducto.Application.Abstractions.Email;
using Microsoft.Extensions.Configuration;

namespace RutaProducto.Infrastructure.Email;

/// <summary>Which sender backs the email seam (<c>Email:Provider</c>).</summary>
public enum EmailProvider
{
    None,
    Smtp,
}

/// <summary>
/// Settings under <c>Email:</c> (and <c>Smtp:</c> with the Smtp provider), validated at startup. An empty provider means
/// <see cref="EmailProvider.None"/>, which sends nothing. Failure messages never echo a configured value.
/// </summary>
public sealed class EmailOptions
{
    public const string ProviderKey = "Email:Provider";

    public const string RequireConfirmedEmailKey = "Email:RequireConfirmedEmail";

    public const string PublicBaseUrlKey = "Email:PublicBaseUrl";

    public EmailProvider Provider { get; private set; } = EmailProvider.None;

    public bool RequireConfirmedEmail { get; private set; }

    /// <summary>Normalised once: no trailing slash. Null unless the provider is Smtp.</summary>
    public Uri? PublicBaseUrl { get; private set; }

    public SmtpOptions? Smtp { get; private set; }

    public AccountEmailSettings ToSettings() => new(Provider == EmailProvider.Smtp, RequireConfirmedEmail, PublicBaseUrl);

    public static EmailOptions BindAndValidate(IConfiguration configuration)
    {
        var options = new EmailOptions
        {
            Provider = ReadProvider(configuration[ProviderKey]),
            RequireConfirmedEmail = SmtpOptions.ReadBoolean(
                configuration[RequireConfirmedEmailKey], RequireConfirmedEmailKey, defaultValue: false),
        };

        if (options.Provider == EmailProvider.None)
        {
            if (options.RequireConfirmedEmail)
            {
                throw new InvalidOperationException(
                    "'Email:RequireConfirmedEmail' is true but 'Email:Provider' is None; set Email__Provider=Smtp.");
            }

            return options;
        }

        options.PublicBaseUrl = ReadPublicBaseUrl(configuration[PublicBaseUrlKey]);
        options.Smtp = SmtpOptions.BindAndValidate(configuration);

        return options;
    }

    private static EmailProvider ReadProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EmailProvider.None;
        }

        if (Enum.TryParse<EmailProvider>(value.Trim(), ignoreCase: true, out var provider) && Enum.IsDefined(provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"'{ProviderKey}' has an unknown value. Valid values are: None, Smtp.");
    }

    private static Uri ReadPublicBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Email:Provider is Smtp but the public base URL is not configured. Set Email__PublicBaseUrl.");
        }

        var trimmed = value.Trim().TrimEnd('/');

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "'Email:PublicBaseUrl' (Email__PublicBaseUrl) must be an absolute http or https URL.");
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !SmtpOptions.IsLocalHost(uri.Host))
        {
            throw new InvalidOperationException(
                "'Email:PublicBaseUrl' (Email__PublicBaseUrl) must use https unless the host is localhost.");
        }

        return uri;
    }
}
