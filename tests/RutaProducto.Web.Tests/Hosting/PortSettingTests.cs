using RutaProducto.Web.Hosting;

namespace RutaProducto.Web.Tests.Hosting;

public sealed class PortSettingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_a_port_nothing_changes(string? port)
    {
        Assert.Null(PortSetting.ResolveUrl(port, configuredUrls: null));
    }

    [Theory]
    [InlineData("8080", "http://0.0.0.0:8080")]
    [InlineData(" 3000 ", "http://0.0.0.0:3000")]
    [InlineData("1", "http://0.0.0.0:1")]
    [InlineData("65535", "http://0.0.0.0:65535")]
    public void A_valid_port_becomes_a_url_on_all_interfaces(string port, string expected)
    {
        Assert.Equal(expected, PortSetting.ResolveUrl(port, configuredUrls: null));
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("https://+:443")]
    public void Explicit_urls_win_over_the_port(string urls)
    {
        Assert.Null(PortSetting.ResolveUrl("8080", urls));
    }

    [Theory]
    [InlineData("abc-secret")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("80.5")]
    [InlineData("+80")]
    public void An_invalid_port_stops_the_startup_naming_the_variable_and_never_echoing_the_value(string port)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PortSetting.ResolveUrl(port, configuredUrls: null));

        Assert.Contains("'PORT'", error.Message);
        Assert.DoesNotContain(port, error.Message);
    }
}
