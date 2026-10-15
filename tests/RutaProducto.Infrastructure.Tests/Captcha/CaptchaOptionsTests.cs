using RutaProducto.Application.Abstractions.Captcha;
using RutaProducto.Infrastructure.Captcha;
using RutaProducto.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Tests.Captcha;

public sealed class CaptchaOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("none")]
    [InlineData("NONE")]
    public async Task Without_a_provider_the_captcha_is_off_and_passes_anything(string? provider)
    {
        var options = CaptchaOptions.BindAndValidate(Config(("Captcha:Provider", provider)));

        Assert.Equal(CaptchaProvider.None, options.Provider);
        Assert.Equal(CaptchaOutcome.Passed, await new NoneCaptchaVerifier().VerifyAsync(string.Empty, CancellationToken.None));
        Assert.Equal(CaptchaOutcome.Passed, await new NoneCaptchaVerifier().VerifyAsync(null, CancellationToken.None));
    }

    [Fact]
    public void An_unknown_provider_lists_the_valid_values()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => CaptchaOptions.BindAndValidate(Config(("Captcha:Provider", "hcaptcha"))));

        Assert.Contains("Captcha:Provider", error.Message);
        Assert.Contains("None", error.Message);
        Assert.Contains("Turnstile", error.Message);
    }

    [Theory]
    [InlineData(null, "S3CRET", "Turnstile__SiteKey")]
    [InlineData("SITE", null, "Turnstile__SecretKey")]
    [InlineData("SITE", " ", "Turnstile__SecretKey")]
    public void Turnstile_requires_both_keys_and_names_the_missing_variable(string? siteKey, string? secretKey, string variable)
    {
        var error = Assert.Throws<InvalidOperationException>(() => CaptchaOptions.BindAndValidate(Config(
            ("Captcha:Provider", "turnstile"), ("Turnstile:SiteKey", siteKey), ("Turnstile:SecretKey", secretKey))));

        Assert.Contains(variable, error.Message);
        Assert.DoesNotContain("S3CRET", error.Message);
    }

    [Fact]
    public void A_configured_secret_never_appears_in_any_startup_message()
    {
        var error = Assert.Throws<InvalidOperationException>(() => CaptchaOptions.BindAndValidate(Config(
            ("Captcha:Provider", "S3CRET"), ("Turnstile:SecretKey", "S3CRET"))));

        Assert.DoesNotContain("S3CRET", error.Message);
    }

    [Fact]
    public void Registration_follows_the_provider()
    {
        using var off = Build();
        Assert.IsType<NoneCaptchaVerifier>(off.GetRequiredService<ICaptchaVerifier>());
        Assert.Null(off.GetRequiredService<CaptchaWidgetSettings>().SiteKey);

        using var on = Build(("Captcha:Provider", "Turnstile"), ("Turnstile:SiteKey", "SITE"), ("Turnstile:SecretKey", "S3CRET"));
        Assert.IsType<TurnstileCaptchaVerifier>(on.GetRequiredService<ICaptchaVerifier>());
        Assert.Equal("SITE", on.GetRequiredService<CaptchaWidgetSettings>().SiteKey);
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => KeyValuePair.Create(value.Key, value.Value)))
            .Build();

    private static ServiceProvider Build(params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Never used to connect; it only satisfies the provider configuration.
                    [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=rutaproducto_tests;User=tests",
                }.Concat(values.Select(value => KeyValuePair.Create(value.Key, value.Value))))
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
