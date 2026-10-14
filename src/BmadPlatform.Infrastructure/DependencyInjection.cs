using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Artifacts.ExportArtifact;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Infrastructure.Artifacts;
using BmadPlatform.Infrastructure.Assistant;
using BmadPlatform.Infrastructure.Export;
using BmadPlatform.Infrastructure.Gemini;
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

                // Explicit on purpose: the registration page lists these rules (PasswordPolicy) and checks them live.
                options.Password.RequiredLength = PasswordPolicy.MinLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager();

        services.AddScoped<IAuthService, IdentityAuthService>();
        services.AddScoped<InitialUserSeeder>();

        services.AddScoped<IInitiativeRepository, InitiativeRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();

        // One switch (Assistant:Provider) selects both the assistant and the drafter. Checked eagerly, like the
        // connection string, so a bad configuration stops the app at startup instead of on the first request.
        if (AssistantProviderSetting.Read(configuration) == AssistantProvider.Gemini)
        {
            var gemini = GeminiOptions.BindAndValidate(configuration);
            services.AddGeminiClient(gemini);

            services.AddGeminiAssistant();

            // Temporary until the drafter that uses the client is registered (slice S4a): the one switch selects both
            // seams, so never fall back silently to the Fake drafter.
            throw new InvalidOperationException(
                "Assistant:Provider is Gemini and its options are valid, and the Gemini assistant exists, but the drafter " +
                "that uses the Gemini client arrives in a later slice. Set Assistant:Provider to Fake.");
        }

        // Deterministic stand-ins. The short pause lets people see the "typing" and loading states.
        services.AddSingleton<IAssistantService>(_ => new FakeAssistantService(TimeSpan.FromMilliseconds(900)));
        services.AddSingleton<IArtifactDrafter>(_ => new FakeArtifactDrafter(TimeSpan.FromMilliseconds(900)));

        // The .md download; a new format would be one more implementation (the PDF is the browser printing the page).
        services.AddSingleton<IArtifactExporter, MarkdownArtifactExporter>();

        return services;
    }
}
