using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting.Internal;
using RutaProducto.Web.Hosting;

namespace RutaProducto.Web.Tests.Hosting;

public sealed class CookieSecurityTests
{
    [Theory]
    [InlineData("Development", CookieSecurePolicy.SameAsRequest)]
    [InlineData("Production", CookieSecurePolicy.Always)]
    [InlineData("Staging", CookieSecurePolicy.Always)]
    [InlineData("Railway", CookieSecurePolicy.Always)]
    public void Cookies_are_always_secure_outside_Development(string environmentName, CookieSecurePolicy expected)
    {
        var environment = new HostingEnvironment { EnvironmentName = environmentName };

        Assert.Equal(expected, CookieSecurity.PolicyFor(environment));
    }
}
