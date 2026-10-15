using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Infrastructure.Persistence;
using RutaProducto.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RutaProducto.Infrastructure.Tests.Identity;

public sealed class PasswordPolicyTests
{
    [Fact]
    public void Identity_enforces_exactly_the_rules_the_registration_page_lists()
    {
        using var provider = Build();

        var password = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.Password;

        // If one of these changes, the list in Register.razor, password-rules.js and PasswordMessages must change with it.
        Assert.Equal(PasswordPolicy.MinLength, password.RequiredLength);
        Assert.True(password.RequireDigit);
        Assert.True(password.RequireLowercase);
        Assert.True(password.RequireUppercase);
        Assert.True(password.RequireNonAlphanumeric);
    }

    [Fact]
    public void The_token_providers_are_named_and_their_lifespans_are_24_and_2_hours()
    {
        using var provider = Build();

        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var reset = provider.GetRequiredService<IOptions<PasswordResetTokenProviderOptions>>().Value;

        Assert.Equal("PasswordReset", identity.Tokens.PasswordResetTokenProvider);
        Assert.True(identity.Tokens.ProviderMap.ContainsKey("PasswordReset"));
        Assert.Equal(TimeSpan.FromHours(24), provider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan);
        Assert.Equal(TimeSpan.FromHours(2), reset.TokenLifespan);
        Assert.Equal("PasswordResetDataProtectorTokenProvider", reset.Name);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void Sign_in_requires_a_confirmed_email_only_when_the_switch_is_on(string? switchValue, bool expected)
    {
        using var provider = Build(switchValue is null
            ? []
            : [
                ("Email:RequireConfirmedEmail", switchValue),
                ("Email:Provider", "Smtp"),
                ("Email:PublicBaseUrl", "https://app.example.com"),
                ("Smtp:Host", "smtp.example.com"),
                ("Smtp:From", "no-reply@example.com"),
            ]);

        Assert.Equal(expected, provider.GetRequiredService<IOptions<IdentityOptions>>().Value.SignIn.RequireConfirmedEmail);
    }

    private static ServiceProvider Build(params (string Key, string? Value)[] values)
    {
        var settings = new Dictionary<string, string?>
        {
            // Never used to connect; it only satisfies the provider configuration.
            [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=rutaproducto_tests;User=tests",
        };

        foreach (var (key, value) in values)
        {
            settings[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // The web host supplies data protection (its cookie authentication adds it); the test host must do it too.
        services.AddDataProtection();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
