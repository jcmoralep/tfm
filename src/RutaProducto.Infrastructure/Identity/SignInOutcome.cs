using RutaProducto.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;

namespace RutaProducto.Infrastructure.Identity;

/// <param name="Result">What the person is told.</param>
/// <param name="CountFailure">True when the attempt must be counted toward lockout (<c>AccessFailedAsync</c>).</param>
internal readonly record struct SignInDecision(AuthenticationResult Result, bool CountFailure = false);

/// <summary>
/// Maps Identity's sign-in result to ours. When confirmation is required, Identity answers NotAllowed before it checks the
/// lockout and without counting a failed password, so the order here is: locked out first, then NotAllowed only when the
/// password matches (no hint about the account otherwise), else count the failure and answer InvalidCredentials.
/// </summary>
internal static class SignInOutcome
{
    public static SignInDecision Resolve(SignInResult result, bool lockedOut, bool passwordMatches)
    {
        if (result.Succeeded)
        {
            return new SignInDecision(AuthenticationResult.Succeeded);
        }

        if (result.IsLockedOut)
        {
            return new SignInDecision(AuthenticationResult.LockedOut);
        }

        if (!result.IsNotAllowed)
        {
            return new SignInDecision(AuthenticationResult.InvalidCredentials);
        }

        if (lockedOut)
        {
            return new SignInDecision(AuthenticationResult.LockedOut);
        }

        return passwordMatches
            ? new SignInDecision(AuthenticationResult.NotAllowed)
            : new SignInDecision(AuthenticationResult.InvalidCredentials, CountFailure: true);
    }
}
