using RutaProducto.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace RutaProducto.Infrastructure.Tests.Identity;

/// <summary>
/// Hand-written user store so the real <see cref="UserManager{TUser}"/> (and its token providers) can run without a database.
/// Only what the account flows use is implemented.
/// </summary>
internal sealed class InMemoryUserStore :
    IUserPasswordStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>,
    IUserSecurityStampStore<ApplicationUser>,
    IUserLockoutStore<ApplicationUser>
{
    private readonly Dictionary<string, ApplicationUser> users = [];

    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        users[user.Id] = user;
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);

    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        users.Remove(user.Id);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(users.GetValueOrDefault(userId));

    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        Task.FromResult(users.Values.FirstOrDefault(user => user.NormalizedUserName == normalizedUserName));

    public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(users.Values.FirstOrDefault(user => user.NormalizedEmail == normalizedEmail));

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) => Set(() => user.UserName = userName);

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) => Set(() => user.NormalizedUserName = normalizedName);

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken) => Set(() => user.PasswordHash = passwordHash);

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash is not null);

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken) => Set(() => user.Email = email);

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) => Set(() => user.EmailConfirmed = confirmed);

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken) => Set(() => user.NormalizedEmail = normalizedEmail);

    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken) => Set(() => user.SecurityStamp = stamp);

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken) => Set(() => user.LockoutEnd = lockoutEnd);

    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) => Set(() => user.AccessFailedCount = 0);

    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken) => Set(() => user.LockoutEnabled = enabled);

    public void Dispose()
    {
    }

    private static Task Set(Action assign)
    {
        assign();
        return Task.CompletedTask;
    }
}
