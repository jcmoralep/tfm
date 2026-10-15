using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace RutaProducto.Infrastructure.Tests.Identity;

public sealed class SignInOutcomeTests
{
    [Theory]
    [InlineData(true, true, AuthenticationResult.LockedOut, false)]
    [InlineData(true, false, AuthenticationResult.LockedOut, false)]
    [InlineData(false, true, AuthenticationResult.NotAllowed, false)]
    [InlineData(false, false, AuthenticationResult.InvalidCredentials, true)]
    public void NotAllowed_is_gated_by_the_lockout_and_by_the_password(
        bool lockedOut, bool passwordMatches, AuthenticationResult expected, bool countsFailure)
    {
        var decision = SignInOutcome.Resolve(SignInResult.NotAllowed, lockedOut, passwordMatches);

        Assert.Equal(expected, decision.Result);
        Assert.Equal(countsFailure, decision.CountFailure);
    }

    [Fact]
    public void Other_results_map_as_before_and_never_count_a_failure()
    {
        Assert.Equal(AuthenticationResult.Succeeded, SignInOutcome.Resolve(SignInResult.Success, false, true).Result);
        Assert.Equal(AuthenticationResult.LockedOut, SignInOutcome.Resolve(SignInResult.LockedOut, false, false).Result);

        var failed = SignInOutcome.Resolve(SignInResult.Failed, false, false);
        Assert.Equal(AuthenticationResult.InvalidCredentials, failed.Result);
        Assert.False(failed.CountFailure);
    }

    [Fact]
    public void The_dummy_hash_is_computed_once_and_no_password_matches_it()
    {
        var hasher = new PasswordHasher<ApplicationUser>();

        var first = DummyPasswordHash.Get(hasher);

        Assert.Same(first, DummyPasswordHash.Get(hasher));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(new ApplicationUser(), first, "Secret-123"));
        DummyPasswordHash.Verify(hasher, "Secret-123");
    }
}
