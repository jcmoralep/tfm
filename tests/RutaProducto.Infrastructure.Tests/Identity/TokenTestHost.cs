using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Tests.Identity;

/// <summary>The real UserManager and token providers over an in-memory store. Each host has its own (ephemeral) key ring.</summary>
internal static class TokenTestHost
{
    public static ServiceProvider Build(
        InMemoryUserStore store, TimeSpan? confirmationLifespan = null, TimeSpan? resetLifespan = null, ILoggerProvider? logs = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (logs is not null)
            {
                builder.AddProvider(logs);
            }
        });
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<IUserStore<ApplicationUser>>(store);
        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true).AddAccountTokenProviders();

        if (confirmationLifespan is not null)
        {
            services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = confirmationLifespan.Value);
        }

        if (resetLifespan is not null)
        {
            services.Configure<PasswordResetTokenProviderOptions>(options => options.TokenLifespan = resetLifespan.Value);
        }

        services.AddScoped<IAccountTokenService, IdentityAccountTokenService>();

        return services.BuildServiceProvider();
    }

    public static async Task<ApplicationUser> AddUserAsync(IServiceProvider provider, string email, bool confirmed = false)
    {
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = confirmed };
        Assert.True((await provider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, "Secret-123")).Succeeded);

        return user;
    }
}
