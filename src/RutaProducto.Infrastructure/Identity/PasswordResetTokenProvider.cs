using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RutaProducto.Infrastructure.Identity;

/// <summary>Own options type so the reset links can live 2 hours while confirmation links live 24.</summary>
public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
}

/// <summary>
/// Data-protection token provider dedicated to password reset. Its own <c>Name</c> is part of the protection purpose, so a
/// confirmation token is never accepted as a reset token. Registered with the other providers; used by the recovery slice.
/// </summary>
public sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "PasswordReset";

    public const string DataProtectionPurpose = "PasswordResetDataProtectorTokenProvider";
}
