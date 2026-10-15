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

        return new AccountToken(user.Id, AccountTokenCodec.Encode(token), user.Email!);
    }

    public async Task<string?> FindStoredEmailAsync(string email, CancellationToken cancellationToken) =>
        (await userManager.FindByEmailAsync(email))?.Email;

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

    public async Task<AccountToken?> CreatePasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        // Confirmed or not, locked out or not: the reset mail goes to the mailbox, which is the proof of ownership.
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return null;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        logger.LogInformation("Password reset token created for user {UserId}", user.Id);

        return new AccountToken(user.Id, AccountTokenCodec.Encode(token), user.Email!);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string userId, string code, string newPassword, CancellationToken cancellationToken)
    {
        var token = AccountTokenCodec.Decode(code);
        var user = token is null || string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);

        if (user is null || token is null)
        {
            logger.LogInformation("Password reset for user {UserId}: {Outcome}", user?.Id, "invalid-link");
            return PasswordResetResult.InvalidLink;
        }

        // The token is verified first and the password policy second; a weak password leaves the token (and the stamp) intact.
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            // Opening the link proved control of the mailbox.
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }

            logger.LogInformation("Password reset for user {UserId}: {Outcome}", user.Id, "reset");
            return PasswordResetResult.Succeeded;
        }

        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            logger.LogInformation("Password reset for user {UserId}: {Outcome}", user.Id, "invalid-link");
            return PasswordResetResult.InvalidLink;
        }

        logger.LogInformation("Password reset for user {UserId}: {Outcome}", user.Id, "invalid-password");

        return PasswordResetResult.InvalidPassword(PasswordMessages.From(result.Errors));
    }
}
