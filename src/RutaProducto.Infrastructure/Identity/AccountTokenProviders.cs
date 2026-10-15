using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Identity;

internal static class AccountTokenProviders
{
    public static readonly TimeSpan ConfirmationLifespan = TimeSpan.FromHours(24);

    public static readonly TimeSpan ResetLifespan = TimeSpan.FromHours(2);

    /// <summary>
    /// Adds the default providers (email confirmation, 24 hours) and the dedicated reset provider (2 hours).
    /// Data protection must be registered by the host (the web host and its cookie authentication do it).
    /// </summary>
    public static IdentityBuilder AddAccountTokenProviders(this IdentityBuilder builder)
    {
        builder.AddDefaultTokenProviders();
        builder.AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = ConfirmationLifespan);
        builder.Services.Configure<PasswordResetTokenProviderOptions>(options =>
        {
            options.Name = PasswordResetTokenProvider.DataProtectionPurpose;
            options.TokenLifespan = ResetLifespan;
        });
        builder.Services.Configure<IdentityOptions>(options =>
            options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName);

        return builder;
    }
}
