using RutaProducto.Web.Authentication;

namespace RutaProducto.Web.Tests.Authentication;

public sealed class LocalRedirectTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/initiatives")]
    [InlineData("/initiatives/42?tab=artifacts")]
    public void Application_relative_paths_are_kept(string returnUrl)
    {
        Assert.Equal(returnUrl, LocalRedirect.Sanitize(returnUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://attacker.example/phish")]
    [InlineData("//attacker.example")]
    [InlineData("/\\attacker.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("initiatives")]
    [InlineData("/home\r\nSet-Cookie: x=y")]
    public void Anything_else_falls_back_to_the_home_page(string? returnUrl)
    {
        Assert.Equal(LocalRedirect.Fallback, LocalRedirect.Sanitize(returnUrl));
    }

    [Theory]
    [InlineData(null, "/registro")]
    [InlineData("https://attacker.example", "/registro")]
    [InlineData("/initiatives/42?tab=artifacts", "/registro?returnUrl=%2Finitiatives%2F42%3Ftab%3Dartifacts")]
    public void The_link_to_the_other_sign_in_page_keeps_only_a_safe_return_address(string? returnUrl, string expected)
    {
        Assert.Equal(expected, LocalRedirect.WithReturnUrl("/registro", returnUrl));
    }
}
