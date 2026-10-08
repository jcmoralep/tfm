namespace BmadPlatform.Application.Abstractions.Authentication;

/// <summary>
/// Extension point that exposes the signed-in user to the application layer.
/// Implemented by the presentation layer, so a future SSO provider only needs a new implementation.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Returns the signed-in user, or <c>null</c> when the request is anonymous.</summary>
    Task<AuthenticatedUser?> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record AuthenticatedUser(string Id, string Email);
