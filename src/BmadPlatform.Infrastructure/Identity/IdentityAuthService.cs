using BmadPlatform.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;

namespace BmadPlatform.Infrastructure.Identity;

/// <summary>
/// <see cref="IAuthService"/> backed by ASP.NET Core Identity cookies.
/// Must run inside an HTTP request (static server rendering), because it writes the auth cookie.
/// </summary>
internal sealed class IdentityAuthService(SignInManager<ApplicationUser> signInManager) : IAuthService
{
    public async Task<AuthenticationResult> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        // Seeded users use their email as user name. A persistent cookie survives closing the browser
        // (it still expires after the configured ExpireTimeSpan).
        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: rememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return AuthenticationResult.Succeeded;
        }

        if (result.IsLockedOut)
        {
            return AuthenticationResult.LockedOut;
        }

        return result.IsNotAllowed ? AuthenticationResult.NotAllowed : AuthenticationResult.InvalidCredentials;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) => signInManager.SignOutAsync();
}
