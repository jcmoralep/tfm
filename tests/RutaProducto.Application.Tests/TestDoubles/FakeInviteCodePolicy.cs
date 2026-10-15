using RutaProducto.Application.Abstractions.Registration;

namespace RutaProducto.Application.Tests.TestDoubles;

/// <summary>Off by default; with an expected code it matches only that exact text and counts how many times it was asked.</summary>
public sealed class FakeInviteCodePolicy(string? expected = null) : IInviteCodePolicy
{
    public int Calls { get; private set; }

    public bool Required => expected is not null;

    public bool Matches(string? typedCode)
    {
        Calls++;

        return expected is null || typedCode == expected;
    }
}
