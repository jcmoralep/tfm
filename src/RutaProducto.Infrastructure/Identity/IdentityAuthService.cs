using RutaProducto.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;

namespace RutaProducto.Infrastructure.Identity;

/// <summary>
/// <see cref="IAuthService"/> backed by ASP.NET Core Identity cookies.
/// Must run inside an HTTP request (static server rendering), because it writes the auth cookie.
/// </summary>
internal sealed class IdentityAuthService(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IPasswordHasher<ApplicationUser> passwordHasher) : IAuthService
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

        if (result.IsNotAllowed)
        {
            return await ResolveNotAllowedAsync(email, password);
        }

        if (!result.IsLockedOut && await userManager.FindByNameAsync(email) is null)
        {
            // An unknown email would otherwise answer faster than a known one; pay the same hash cost.
            DummyPasswordHash.Verify(passwordHasher, password);
        }

        return SignInOutcome.Resolve(result, lockedOut: false, passwordMatches: false).Result;
    }

    // With confirmation required Identity answers NotAllowed before the lockout check and without counting a wrong
    // password, so the order is decided here (see SignInOutcome).
    private async Task<AuthenticationResult> ResolveNotAllowedAsync(string email, string password)
    {
        var user = await userManager.FindByNameAsync(email);

        if (user is null)
        {
            return AuthenticationResult.InvalidCredentials;
        }

        var lockedOut = await userManager.IsLockedOutAsync(user);
        var passwordMatches = !lockedOut && await userManager.CheckPasswordAsync(user, password);
        var decision = SignInOutcome.Resolve(SignInResult.NotAllowed, lockedOut, passwordMatches);

        if (decision.CountFailure)
        {
            await userManager.AccessFailedAsync(user);

            // The failure that reaches the limit locks the account, as it does in the normal sign-in.
            if (await userManager.IsLockedOutAsync(user))
            {
                return AuthenticationResult.LockedOut;
            }
        }

        return decision.Result;
    }

    public async Task<RegistrationResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        // The address starts unconfirmed (only seeded users are confirmed); the confirmation email flips it when
        // Email:RequireConfirmedEmail is on.
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
