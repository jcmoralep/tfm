using RutaProducto.Infrastructure.Email;
using Microsoft.Extensions.Configuration;

namespace RutaProducto.Infrastructure.Tests.Email;

public sealed class EmailOptionsTests
{
    private const string Password = "P4SSW0RD-VALUE";

    private static readonly (string Key, string? Value)[] Valid =
    [
        ("Email:Provider", "Smtp"),
        ("Email:PublicBaseUrl", "https://app.example.com"),
        ("Smtp:Host", "smtp.example.com"),
        ("Smtp:From", "no-reply@example.com"),
        ("Smtp:Username", "user"),
        ("Smtp:Password", Password),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("none")]
    [InlineData("NONE")]
    public void Without_a_provider_email_is_off_and_no_smtp_setting_is_read(string? provider)
    {
        var options = EmailOptions.BindAndValidate(Config(("Email:Provider", provider), ("Smtp:Port", "abc")));

        Assert.Equal(EmailProvider.None, options.Provider);
        Assert.Null(options.Smtp);
        Assert.Equal(new(false, false, null), options.ToSettings());
    }

    [Theory]
    [InlineData("Smtp:Host", null, "Smtp__Host")]
    [InlineData("Smtp:From", null, "Smtp__From")]
    [InlineData("Smtp:From", "not-an-address", "Smtp__From")]
    [InlineData("Email:PublicBaseUrl", null, "Email__PublicBaseUrl")]
    [InlineData("Email:PublicBaseUrl", "/relative/path", "Email__PublicBaseUrl")]
    [InlineData("Email:PublicBaseUrl", "http://app.example.com", "Email__PublicBaseUrl")]
    [InlineData("Smtp:Port", "abc", "Smtp:Port")]
    [InlineData("Smtp:Port", "0", "Smtp:Port")]
    [InlineData("Smtp:Port", "70000", "Smtp:Port")]
    [InlineData("Smtp:Port", "465", "Smtp:Port")]
    [InlineData("Smtp:EnableSsl", "false", "Smtp:EnableSsl")]
    [InlineData("Smtp:EnableSsl", "maybe", "Smtp:EnableSsl")]
    [InlineData("Smtp:TimeoutSeconds", "121", "Smtp:TimeoutSeconds")]
    [InlineData("Smtp:Password", null, "Smtp__Password")]
    [InlineData("Email:RequireConfirmedEmail", "maybe", "Email:RequireConfirmedEmail")]
    public void An_invalid_smtp_configuration_stops_the_app_naming_the_setting_and_never_a_value(string key, string? value, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(() => EmailOptions.BindAndValidate(Config(Override(key, value))));

        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain(Password, error.Message);
        Assert.DoesNotContain("not-an-address", error.Message);
        Assert.DoesNotContain("maybe", error.Message);
    }

    [Fact]
    public void A_complete_configuration_passes_with_defaults_and_a_normalised_base_url()
    {
        var options = EmailOptions.BindAndValidate(Config(Override("Email:PublicBaseUrl", "https://app.example.com/ruta/")));

        Assert.Equal(EmailProvider.Smtp, options.Provider);
        Assert.False(options.RequireConfirmedEmail);
        Assert.Equal("https://app.example.com/ruta", options.PublicBaseUrl!.ToString().TrimEnd('/'));
        Assert.Equal(587, options.Smtp!.Port);
        Assert.True(options.Smtp.EnableSsl);
        Assert.Equal(30, options.Smtp.TimeoutSeconds);
        Assert.Equal("Ruta de Producto", options.Smtp.FromName);
        Assert.Equal(new(true, false, options.PublicBaseUrl), options.ToSettings());
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void A_local_sink_needs_no_tls_username_or_password(string host)
    {
        var options = EmailOptions.BindAndValidate(Config(
            ("Email:Provider", "Smtp"),
            ("Email:PublicBaseUrl", "http://localhost:5000"),
            ("Smtp:Host", host),
            ("Smtp:Port", "1025"),
            ("Smtp:EnableSsl", "false"),
            ("Smtp:From", "no-reply@ruta.local")));

        Assert.False(options.Smtp!.EnableSsl);
        Assert.Null(options.Smtp.Username);
        Assert.Null(options.Smtp.Password);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("None")]
    public void Requiring_confirmation_without_smtp_stops_the_app(string? provider)
    {
        var error = Assert.Throws<InvalidOperationException>(() => EmailOptions.BindAndValidate(Config(
            ("Email:Provider", provider), ("Email:RequireConfirmedEmail", "true"))));

        Assert.Contains("Email:RequireConfirmedEmail", error.Message);
        Assert.Contains("Email:Provider", error.Message);
    }

    [Fact]
    public void Requiring_confirmation_with_smtp_passes()
    {
        var options = EmailOptions.BindAndValidate(Config(Override("Email:RequireConfirmedEmail", "true")));

        Assert.True(options.ToSettings().RequireConfirmedEmail);
    }

    [Fact]
    public void An_unknown_provider_lists_the_valid_values()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => EmailOptions.BindAndValidate(Config(("Email:Provider", "Sendgrid"))));

        Assert.Contains("Email:Provider", error.Message);
        Assert.Contains("None", error.Message);
        Assert.Contains("Smtp", error.Message);
    }

    private static IEnumerable<(string Key, string? Value)> Override(string key, string? value) =>
        Valid.Where(setting => setting.Key != key).Append((key, value));

    private static IConfiguration Config(IEnumerable<(string Key, string? Value)> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => KeyValuePair.Create(value.Key, value.Value)))
            .Build();

    private static IConfiguration Config(params (string Key, string? Value)[] values) => Config(values.AsEnumerable());
}
