using BmadPlatform.Application.Abstractions.Authentication;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>Current user whose identity tests can switch; <c>null</c> simulates an anonymous request.</summary>
public sealed class FakeCurrentUser(string? userId = "user-a") : ICurrentUser
{
    public string? UserId { get; set; } = userId;

    public Task<AuthenticatedUser?> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(UserId is null ? null : new AuthenticatedUser(UserId, $"{UserId}@example.com"));
}
