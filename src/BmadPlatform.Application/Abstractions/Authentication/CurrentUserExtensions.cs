namespace BmadPlatform.Application.Abstractions.Authentication;

public static class CurrentUserExtensions
{
    /// <summary>
    /// Returns the signed-in user's id. Pages are <c>[Authorize]</c>, so a missing user is a defect,
    /// not a user error, and fails with <see cref="InvalidOperationException"/>.
    /// </summary>
    public static async Task<string> GetRequiredIdAsync(this ICurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var user = await currentUser.GetAsync(cancellationToken);

        return user?.Id ?? throw new InvalidOperationException("This operation requires an authenticated user.");
    }
}
