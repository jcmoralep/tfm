using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Abstractions.Storage;
using RutaProducto.Application.Features.Artifacts.ExportArtifact;
using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Context;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Infrastructure.Artifacts;
using RutaProducto.Infrastructure.Assistant;
using RutaProducto.Infrastructure.Context;
using RutaProducto.Infrastructure.Export;
using RutaProducto.Infrastructure.Gemini;
using RutaProducto.Infrastructure.Identity;
using RutaProducto.Infrastructure.Identity.Seeding;
using RutaProducto.Infrastructure.Initiatives;
using RutaProducto.Infrastructure.Persistence;
using RutaProducto.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure;

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
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();

        // Checked eagerly, like the connection string: S3 or a bad size stops the app at startup. Only local disk
        // exists for now; the per-file cap travels to the UI inside ContextLimits.
        var storage = StorageOptions.BindAndValidate(configuration);
        services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(storage.LocalRootPath));
        services.AddSingleton(new ContextLimits(storage.MaxFileBytes));

        // One switch (Assistant:Provider) selects both the assistant and the drafter. Checked eagerly, like the
        // connection string, so a bad configuration stops the app at startup instead of on the first request.
        if (AssistantProviderSetting.Read(configuration) == AssistantProvider.Gemini)
        {
            var gemini = GeminiOptions.BindAndValidate(configuration);
            services.AddGeminiClient(gemini);

            // The one switch selects both seams: a model-backed document never falls back to the Fake drafter.
            services.AddGeminiAssistant();
            services.AddGeminiDrafter();
        }
        else
        {
            // Deterministic stand-ins. The short pause lets people see the "typing" and loading states.
            services.AddSingleton<IAssistantService>(_ => new FakeAssistantService(TimeSpan.FromMilliseconds(900)));
            services.AddSingleton<IArtifactDrafter>(_ => new FakeArtifactDrafter(TimeSpan.FromMilliseconds(900)));
        }

        // The .md download; a new format would be one more implementation (the PDF is the browser printing the page).
        services.AddSingleton<IArtifactExporter, MarkdownArtifactExporter>();

        return services;
    }
}
