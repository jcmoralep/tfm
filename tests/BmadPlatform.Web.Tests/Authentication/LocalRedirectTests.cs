using BmadPlatform.Web.Authentication;

namespace BmadPlatform.Web.Tests.Authentication;

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
}
