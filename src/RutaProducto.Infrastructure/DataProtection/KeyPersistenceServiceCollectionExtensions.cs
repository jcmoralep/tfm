using RutaProducto.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.DataProtection;

public static class KeyPersistenceServiceCollectionExtensions
{
    /// <summary>Fixed so every replica and every redeploy shares one key ring and one purpose hierarchy.</summary>
    public const string ApplicationName = "RutaProducto";

    /// <summary>
    /// With <see cref="KeyStoreProvider.Database"/> the keys are stored (unencrypted at rest) in the <c>dp_keys</c> table so
    /// sign-in cookies and email links survive a redeploy. With <see cref="KeyStoreProvider.Default"/> nothing is registered
    /// and the framework keeps its own behavior.
    /// </summary>
    public static IServiceCollection AddKeyPersistence(this IServiceCollection services, DataProtectionOptions options)
    {
        if (options.Provider == KeyStoreProvider.Database)
        {
            services
                .AddDataProtection()
                .SetApplicationName(ApplicationName)
                .PersistKeysToDbContext<ApplicationDbContext>();
        }

        return services;
    }
}
