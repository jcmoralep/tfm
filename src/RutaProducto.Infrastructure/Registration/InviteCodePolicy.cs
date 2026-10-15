using System.Security.Cryptography;
using System.Text;
using RutaProducto.Application.Abstractions.Registration;

namespace RutaProducto.Infrastructure.Registration;

/// <summary>Compares the typed code with the configured one: trimmed, case-sensitive, constant time (SHA-256 then FixedTimeEquals).</summary>
public sealed class InviteCodePolicy : IInviteCodePolicy
{
    private readonly byte[]? expectedHash;

    public InviteCodePolicy(string? configuredCode)
    {
        var code = configuredCode?.Trim();
        expectedHash = string.IsNullOrEmpty(code) ? null : Hash(code);
    }

    public bool Required => expectedHash is not null;

    public bool Matches(string? typedCode)
    {
        if (expectedHash is null)
        {
            return true;
        }

        return CryptographicOperations.FixedTimeEquals(expectedHash, Hash((typedCode ?? string.Empty).Trim()));
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
