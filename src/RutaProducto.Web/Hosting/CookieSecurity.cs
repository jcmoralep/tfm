namespace RutaProducto.Web.Hosting;

/// <summary>
/// Rule for the session cookie: <c>Secure</c> always outside Development. Behind Railway's TLS-terminating proxy the container
/// sees plain http, so the framework default (<c>SameAsRequest</c>) would issue it without <c>Secure</c> unless forwarded headers
/// restore the scheme. Development keeps <c>SameAsRequest</c> so a local <c>dotnet run</c> over http://localhost keeps working.
/// It is NOT applied to the antiforgery cookie: with <c>Always</c> the framework refuses to issue that cookie over plain http and
/// every form page answers 500; the antiforgery cookie follows the request scheme, so it is <c>Secure</c> once
/// <c>ForwardedHeaders:Enabled</c> restores https.
/// </summary>
internal static class CookieSecurity
{
    public static CookieSecurePolicy PolicyFor(IHostEnvironment environment)
        => environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
}
