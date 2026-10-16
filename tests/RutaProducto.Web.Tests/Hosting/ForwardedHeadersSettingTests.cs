using RutaProducto.Web.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RutaProducto.Web.Tests.Hosting;

public sealed class ForwardedHeadersSettingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("FALSE")]
    public void It_is_off_by_default_and_registers_nothing(string? value)
    {
        var setting = ForwardedHeadersSetting.BindAndValidate(Config(value));
        var services = setting.AddForwardedHeadersIfEnabled(new ServiceCollection());

        Assert.False(setting.Enabled);
        Assert.Empty(services);
    }

    [Theory]
    [InlineData("true")]
    [InlineData(" True ")]
    public void When_on_it_trusts_the_for_and_proto_headers_from_any_proxy(string value)
    {
        var setting = ForwardedHeadersSetting.BindAndValidate(Config(value));
        using var provider = setting.AddForwardedHeadersIfEnabled(new ServiceCollection()).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.True(setting.Enabled);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Theory]
    [InlineData("maybe-secret")]
    [InlineData("1")]
    [InlineData("yes")]
    public void An_invalid_value_stops_the_startup_naming_the_key_and_never_echoing_the_value(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ForwardedHeadersSetting.BindAndValidate(Config(value)));

        Assert.Contains("'ForwardedHeaders:Enabled'", error.Message);
        Assert.DoesNotContain(value, error.Message);
    }

    private static IConfiguration Config(string? value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([KeyValuePair.Create<string, string?>("ForwardedHeaders:Enabled", value)])
            .Build();
}
