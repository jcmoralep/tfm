using RutaProducto.Web.Security;
using Microsoft.Extensions.Configuration;

namespace RutaProducto.Web.Tests.Security;

public sealed class RateLimitOptionsTests
{
    [Fact]
    public void Absent_settings_give_the_disabled_defaults()
    {
        var options = RateLimitOptions.BindAndValidate(Build());

        Assert.False(options.Enabled);
        Assert.Equal(10, options.PermitLimit);
        Assert.Equal(60, options.WindowSeconds);
    }

    [Fact]
    public void Enabled_with_explicit_numbers_binds_them()
    {
        var options = RateLimitOptions.BindAndValidate(Build(("Enabled", "true"), ("PermitLimit", "5"), ("WindowSeconds", "30")));

        Assert.True(options.Enabled);
        Assert.Equal(5, options.PermitLimit);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Window);
    }

    [Theory]
    [InlineData("PermitLimit", "0")]
    [InlineData("PermitLimit", "-1")]
    [InlineData("PermitLimit", "abc-secret")]
    [InlineData("PermitLimit", "1001")]
    [InlineData("WindowSeconds", "0")]
    [InlineData("WindowSeconds", "3601")]
    public void Bad_numbers_fail_only_when_enabled_and_never_echo_the_value(string key, string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            RateLimitOptions.BindAndValidate(Build(("Enabled", "true"), (key, value))));

        Assert.Contains($"'RateLimiting:{key}'", error.Message);
        Assert.DoesNotContain(value, error.Message.Replace("1000", string.Empty).Replace("3600", string.Empty));

        // The same value with the limiter off is ignored.
        Assert.False(RateLimitOptions.BindAndValidate(Build(("Enabled", "false"), (key, value))).Enabled);
    }

    [Fact]
    public void A_non_boolean_enabled_throws_without_echoing_it()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RateLimitOptions.BindAndValidate(Build(("Enabled", "maybe"))));

        Assert.Contains("'RateLimiting:Enabled'", error.Message);
        Assert.DoesNotContain("maybe", error.Message);
    }

    private static IConfiguration Build(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>($"RateLimiting:{v.Key}", v.Value)))
            .Build();
}
