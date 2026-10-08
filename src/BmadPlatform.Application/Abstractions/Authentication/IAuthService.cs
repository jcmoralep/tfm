namespace BmadPlatform.Application.Abstractions.Authentication;

/// <summary>
/// Extension point for authentication. The current implementation uses ASP.NET Core Identity
/// with email and password; SSO or role-aware implementations can replace it later.
/// </summary>
public interface IAuthService
{
    Task<AuthenticationResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

public enum AuthenticationResult
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
    NotAllowed,
}
