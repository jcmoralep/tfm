using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BmadPlatform.Infrastructure.Tests.Identity;

public sealed class PasswordPolicyTests
{
    [Fact]
    public void Identity_enforces_exactly_the_rules_the_registration_page_lists()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Never used to connect; it only satisfies the provider configuration.
                [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=bmadplatform_tests;User=tests",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var password = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.Password;

        // If one of these changes, the list in Register.razor, password-rules.js and PasswordMessages must change with it.
        Assert.Equal(PasswordPolicy.MinLength, password.RequiredLength);
        Assert.True(password.RequireDigit);
        Assert.True(password.RequireLowercase);
        Assert.True(password.RequireUppercase);
        Assert.True(password.RequireNonAlphanumeric);
    }
}
