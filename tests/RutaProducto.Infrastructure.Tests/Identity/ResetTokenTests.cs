using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Tests.Identity;

public sealed class ResetTokenTests
{
    private const string Email = "ana@example.com";
    private const string NewPassword = "Brand-New-456";

    [Fact]
    public async Task A_valid_token_resets_the_password_once()
    {
        using var provider = TokenTestHost.Build(new InMemoryUserStore());
        using var scope = provider.CreateScope();
        var user = await TokenTestHost.AddUserAsync(scope.ServiceProvider, Email, confirmed: true);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokens = scope.ServiceProvider.GetRequiredService<IAccountTokenService>();

        var token = await tokens.CreatePasswordResetAsync(Email, CancellationToken.None);

        Assert.NotNull(token);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(PasswordResetStatus.Succeeded, (await tokens.ResetPasswordAsync(token.UserId, token.Code, NewPassword, CancellationToken.None)).Status);
        Assert.False(await users.CheckPasswordAsync(user, "Secret-123"));
        Assert.True(await users.CheckPasswordAsync(user, NewPassword));

        // The security stamp rotated, so the same link is spent.
        Assert.Equal(PasswordResetStatus.InvalidLink, (await tokens.ResetPasswordAsync(token.UserId, token.Code, "Another-789", CancellationToken.None)).Status);
        Assert.True(await users.CheckPasswordAsync(user, NewPassword));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Any_existing_account_gets_a_token_and_a_reset_confirms_the_address(bool confirmed)
    {
        using var provider = TokenTestHost.Build(new InMemoryUserStore());
        using var scope = provider.CreateScope();
        var user = await TokenTestHost.AddUserAsync(scope.ServiceProvider, Email, confirmed);
        var tokens = scope.ServiceProvider.GetRequiredService<IAccountTokenService>();

        var token = await tokens.CreatePasswordResetAsync(Email, CancellationToken.None);
        Assert.NotNull(token);
        await tokens.ResetPasswordAsync(token.UserId, token.Code, NewPassword, CancellationToken.None);

        Assert.True(user.EmailConfirmed);
        Assert.Null(await tokens.CreatePasswordResetAsync("nobody@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task Tokens_and_lookups_carry_the_stored_address_not_the_typed_one()
    {
        using var provider = TokenTestHost.Build(new InMemoryUserStore());
        using var scope = provider.CreateScope();
        await TokenTestHost.AddUserAsync(scope.ServiceProvider, Email);
        var tokens = scope.ServiceProvider.GetRequiredService<IAccountTokenService>();

        var reset = await tokens.CreatePasswordResetAsync("ANA@Example.COM", CancellationToken.None);
        var confirmation = await tokens.CreateEmailConfirmationAsync("ANA@Example.COM", CancellationToken.None);

        Assert.Equal(Email, reset?.Email);
        Assert.Equal(Email, confirmation?.Email);
        Assert.Equal(Email, await tokens.FindStoredEmailAsync("ANA@Example.COM", CancellationToken.None));
        Assert.Null(await tokens.FindStoredEmailAsync("nobody@example.com", CancellationToken.None));
    }

    [Theory]
    [InlineData("altered")]
    [InlineData("not-base64url")]
    [InlineData("empty")]
    [InlineData("unknown-user")]
    [InlineData("confirmation-token")]
    [InlineData("another-key-ring")]
    [InlineData("expired")]
    public async Task Every_bad_token_gives_the_same_invalid_link_and_never_throws(string scenario)
    {
        var store = new InMemoryUserStore();
        using var issuer = TokenTestHost.Build(store, resetLifespan: scenario == "expired" ? TimeSpan.FromMilliseconds(1) : null);
        using var issuerScope = issuer.CreateScope();
        var user = await TokenTestHost.AddUserAsync(issuerScope.ServiceProvider, Email);
        var issuerTokens = issuerScope.ServiceProvider.GetRequiredService<IAccountTokenService>();
        var token = scenario == "confirmation-token"
            ? await issuerTokens.CreateEmailConfirmationAsync(Email, CancellationToken.None)
            : await issuerTokens.CreatePasswordResetAsync(Email, CancellationToken.None);
        Assert.NotNull(token);

        using var checker = scenario == "another-key-ring" ? TokenTestHost.Build(store) : issuer;
        using var checkerScope = checker.CreateScope();
        var tokens = checkerScope.ServiceProvider.GetRequiredService<IAccountTokenService>();

        var (userId, code) = scenario switch
        {
            "altered" => (user.Id, token.Code[..5] + (token.Code[5] == 'A' ? 'B' : 'A') + token.Code[6..]),
            "not-base64url" => (user.Id, "***not base64***"),
            "empty" => (user.Id, string.Empty),
            "unknown-user" => ("no-such-user", token.Code),
            _ => (user.Id, token.Code),
        };

        if (scenario == "expired")
        {
            await Task.Delay(100);
        }

        var result = await tokens.ResetPasswordAsync(userId, code, NewPassword, CancellationToken.None);

        Assert.Equal(PasswordResetStatus.InvalidLink, result.Status);
        Assert.Empty(result.PasswordErrors);
        Assert.False(user.EmailConfirmed);
        Assert.True(await checkerScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CheckPasswordAsync(user, "Secret-123"));
    }

    [Fact]
    public async Task A_weak_password_is_reported_in_spanish_and_leaves_the_link_usable()
    {
        using var provider = TokenTestHost.Build(new InMemoryUserStore());
        using var scope = provider.CreateScope();
        await TokenTestHost.AddUserAsync(scope.ServiceProvider, Email);
        var tokens = scope.ServiceProvider.GetRequiredService<IAccountTokenService>();
        var token = await tokens.CreatePasswordResetAsync(Email, CancellationToken.None);
        Assert.NotNull(token);

        var weak = await tokens.ResetPasswordAsync(token.UserId, token.Code, "abc", CancellationToken.None);

        Assert.Equal(PasswordResetStatus.InvalidPassword, weak.Status);
        Assert.Contains("La contraseña debe incluir una letra mayúscula.", weak.PasswordErrors);
        Assert.All(weak.PasswordErrors, message => Assert.StartsWith("La contraseña", message));

        var retry = await tokens.ResetPasswordAsync(token.UserId, token.Code, NewPassword, CancellationToken.None);

        Assert.Equal(PasswordResetStatus.Succeeded, retry.Status);
    }
}
