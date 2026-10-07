using System.Security.Claims;
using BmadPlatform.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

namespace BmadPlatform.Web.Authentication;

/// <summary>
/// Reads the signed-in user from the Blazor authentication state, which works both during
/// static rendering (HTTP request) and inside an interactive circuit (no HttpContext).
/// </summary>
internal sealed class CurrentUser(AuthenticationStateProvider authenticationStateProvider) : ICurrentUser
{
    public async Task<AuthenticatedUser?> GetAsync(CancellationToken cancellationToken = default)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var principal = state.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity.Name;

        return id is null || email is null ? null : new AuthenticatedUser(id, email);
    }
}
