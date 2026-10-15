using RutaProducto.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Identity;

/// <summary><see cref="IAccountTokenService"/> over Identity's data-protection token providers. Logs the user id and the outcome only.</summary>
internal sealed class IdentityAccountTokenService(
    UserManager<ApplicationUser> userManager,
    ILogger<IdentityAccountTokenService> logger) : IAccountTokenService
{
    public async Task<AccountToken?> CreateEmailConfirmationAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || await userManager.IsEmailConfirmedAsync(user))
        {
            return null;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        logger.LogInformation("Confirmation token created for user {UserId}", user.Id);

        return new AccountToken(user.Id, AccountTokenCodec.Encode(token));
    }

    public async Task<bool> ConfirmEmailAsync(string userId, string code, CancellationToken cancellationToken)
    {
        var token = AccountTokenCodec.Decode(code);
        var user = token is null || string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);

        // Malformed code, unknown user, altered, expired or foreign-key-ring token: all the same answer.
        var confirmed = user is not null
            && token is not null
            && (await userManager.ConfirmEmailAsync(user, token)).Succeeded;

        logger.LogInformation("Email confirmation for user {UserId}: {Outcome}", user?.Id, confirmed ? "confirmed" : "rejected");

        return confirmed;
    }
}
