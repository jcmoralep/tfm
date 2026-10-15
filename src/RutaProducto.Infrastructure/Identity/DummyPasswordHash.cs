using Microsoft.AspNetCore.Identity;

namespace RutaProducto.Infrastructure.Identity;

/// <summary>
/// A sign-in for an unknown email would return before hashing and so answer faster than one for a known email. Verifying
/// against a fixed hash makes both pay the hash cost. The hash is computed once, lazily, with the real hasher.
/// </summary>
internal static class DummyPasswordHash
{
    private static readonly ApplicationUser DummyUser = new();

    private static string? hash;

    public static string Get(IPasswordHasher<ApplicationUser> hasher) =>
        hash ??= hasher.HashPassword(DummyUser, "unused-password-for-timing-only");

    public static void Verify(IPasswordHasher<ApplicationUser> hasher, string password) =>
        hasher.VerifyHashedPassword(DummyUser, Get(hasher), password);
}
