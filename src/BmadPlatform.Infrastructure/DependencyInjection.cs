using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Artifacts.ExportArtifact;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Infrastructure.Artifacts;
using BmadPlatform.Infrastructure.Assistant;
using BmadPlatform.Infrastructure.Export;
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
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();

        // Deterministic stand-in until the Gemini implementation arrives (PROPUESTA-MVP step 5).
        // The short pause lets people see the "typing" indicator; the real model will take longer.
        services.AddSingleton<IAssistantService>(_ => new FakeAssistantService(TimeSpan.FromMilliseconds(900)));

        // Same idea for the documents (PROPUESTA-MVP step 5 replaces it); the pause shows the loading state.
        services.AddSingleton<IArtifactDrafter>(_ => new FakeArtifactDrafter(TimeSpan.FromMilliseconds(900)));

        // The .md download; a new format would be one more implementation (the PDF is the browser printing the page).
        services.AddSingleton<IArtifactExporter, MarkdownArtifactExporter>();

        return services;
    }
}
