using RutaProducto.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;

namespace RutaProducto.Infrastructure.Identity;

/// <summary>
/// <see cref="IAuthService"/> backed by ASP.NET Core Identity cookies.
/// Must run inside an HTTP request (static server rendering), because it writes the auth cookie.
/// </summary>
internal sealed class IdentityAuthService(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : IAuthService
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

    public async Task<RegistrationResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Open registration without email verification (for now), so the address is not marked as confirmed:
        // a later verification step can tell these accounts apart from the seeded ones.
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = false };
        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            return RegistrationResult.Succeeded;
        }

        if (result.Errors.Any(error => error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return RegistrationResult.EmailTaken;
        }

        return RegistrationResult.InvalidPassword(PasswordMessages.From(result.Errors));
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) => signInManager.SignOutAsync();
}
