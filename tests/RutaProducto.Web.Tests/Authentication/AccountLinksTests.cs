using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Web.Authentication;

namespace RutaProducto.Web.Tests.Authentication;

public sealed class AccountLinksTests
{
    [Theory]
    [InlineData("https://app.example.com", "https://app.example.com/confirmar-correo?usuario=u%201&codigo=a-b_c")]
    [InlineData("https://app.example.com/", "https://app.example.com/confirmar-correo?usuario=u%201&codigo=a-b_c")]
    [InlineData("https://app.example.com/ruta", "https://app.example.com/ruta/confirmar-correo?usuario=u%201&codigo=a-b_c")]
    [InlineData("http://localhost:5001", "http://localhost:5001/confirmar-correo?usuario=u%201&codigo=a-b_c")]
    public void The_confirmation_link_is_the_configured_base_plus_the_path_and_an_escaped_query(string baseUrl, string expected)
    {
        var links = new AccountLinks(new AccountEmailSettings(true, true, new Uri(baseUrl)));

        Assert.Equal(expected, links.EmailConfirmation(new AccountToken("u 1", "a-b_c")).AbsoluteUri);
    }

    [Fact]
    public void The_login_and_forgot_links_use_the_same_base_and_no_request_host_exists_to_leak()
    {
        var links = new AccountLinks(new AccountEmailSettings(true, true, new Uri("https://app.example.com/ruta")));

        Assert.Equal("https://app.example.com/ruta/login", links.Login().AbsoluteUri);
        Assert.Equal("https://app.example.com/ruta/recuperar-contrasena", links.ForgotPassword().AbsoluteUri);
    }

    [Fact]
    public void Without_a_public_base_url_building_a_link_fails_loudly()
    {
        var links = new AccountLinks(new AccountEmailSettings(false, false, null));

        Assert.Throws<InvalidOperationException>(links.Login);
    }
}
