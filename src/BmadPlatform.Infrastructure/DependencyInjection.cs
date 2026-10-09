using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Infrastructure.Assistant;
using BmadPlatform.Infrastructure.Identity;
using BmadPlatform.Infrastructure.Identity.Seeding;
using BmadPlatform.Infrastructure.Initiatives;
using BmadPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BmadPlatform.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence and Identity stores. The host must register the authentication
    /// scheme (Identity cookies) because it owns the login and access-denied routes.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(MySqlDatabase.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{MySqlDatabase.ConnectionStringName}' is not configured. " +
                $"Set the environment variable ConnectionStrings__{MySqlDatabase.ConnectionStringName}.");
        }

        var serverVersion = MySqlDatabase.ParseServerVersion(configuration[MySqlDatabase.ServerVersionKey]);

        // Blazor Server circuits outlive a request: components must use IDbContextFactory, never a DbContext.
        services.AddDbContextFactory<ApplicationDbContext>(
            options => options.UseApplicationMySql(connectionString, serverVersion));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager();

        services.AddScoped<IAuthService, IdentityAuthService>();
        services.AddScoped<InitialUserSeeder>();

        services.AddScoped<IInitiativeRepository, InitiativeRepository>();

        // Deterministic stand-in until the Gemini implementation arrives (PROPUESTA-MVP step 5).
        services.AddSingleton<IAssistantService, FakeAssistantService>();

        return services;
    }
}
