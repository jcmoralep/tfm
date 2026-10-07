namespace BmadPlatform.Infrastructure.Identity.Seeding;

/// <summary>
/// One initial user, bound from the <c>SeedUsers</c> configuration section.
/// Values come from environment variables or user-secrets, never from files in the repository:
/// <c>SeedUsers__0__Email</c>, <c>SeedUsers__0__Password</c>, <c>SeedUsers__1__Email</c>, ...
/// </summary>
public sealed class SeedUser
{
    public const string SectionName = "SeedUsers";

    public string? Email { get; init; }

    public string? Password { get; init; }
}
