using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Email;

namespace RutaProducto.Web.Authentication;

/// <summary>
/// Builds the links placed in emails from <c>Email:PublicBaseUrl</c> only. The request host is never used: a forged Host
/// header could otherwise point an emailed link, and the token in it, at another site.
/// </summary>
internal sealed class AccountLinks(AccountEmailSettings settings) : IAccountLinks
{
    public Uri EmailConfirmation(AccountToken token) =>
        Build(AuthenticationEndpoints.ConfirmEmailPath, $"?usuario={Uri.EscapeDataString(token.UserId)}&codigo={Uri.EscapeDataString(token.Code)}");

    public Uri Login() => Build(AuthenticationEndpoints.LoginPath);

    public Uri ForgotPassword() => Build(AuthenticationEndpoints.ForgotPasswordPath);

    private Uri Build(string path, string query = "")
    {
        var baseUrl = settings.PublicBaseUrl
            ?? throw new InvalidOperationException("Email:PublicBaseUrl is not configured; account links cannot be built.");

        return new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + path + query, UriKind.Absolute);
    }
}
