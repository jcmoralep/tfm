using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Tests.Identity;

public sealed class ConfirmationTokenTests
{
    private const string Email = "ana@example.com";

    [Fact]
    public async Task A_valid_token_confirms_the_address_once_it_is_unconfirmed()
    {
        using var provider = TokenTestHost.Build(new InMemoryUserStore());
        using var scope = provider.CreateScope();
        var user = await TokenTestHost.AddUserAsync(scope.ServiceProvider, Email);
        var tokens = scope.ServiceProvider.GetRequiredService<IAccountTokenService>();

        var token = await tokens.CreateEmailConfirmationAsync(Email, CancellationToken.None);

        Assert.NotNull(token);
        Assert.Equal(user.Id, token.UserId);
        Assert.True(await tokens.ConfirmEmailAsync(token.UserId, token.Code, CancellationToken.None));
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task No_token_is_created_for_an_unknown_or_an_already_confirmed_address()
    {
        using var provider = TokenTestHost.Build(new InMemoryUserStore());
        using var scope = provider.CreateScope();
        await TokenTestHost.AddUserAsync(scope.ServiceProvider, Email, confirmed: true);
        var tokens = scope.ServiceProvider.GetRequiredService<IAccountTokenService>();

        Assert.Null(await tokens.CreateEmailConfirmationAsync(Email, CancellationToken.None));
        Assert.Null(await tokens.CreateEmailConfirmationAsync("nobody@example.com", CancellationToken.None));
    }

    [Theory]
    [InlineData("altered")]
    [InlineData("not-base64url")]
    [InlineData("empty")]
    [InlineData("unknown-user")]
    [InlineData("another-key-ring")]
    [InlineData("expired")]
    public async Task Every_bad_token_gives_the_same_false_and_never_throws(string scenario)
    {
        var store = new InMemoryUserStore();
        using var issuer = TokenTestHost.Build(store, scenario == "expired" ? TimeSpan.FromMilliseconds(1) : null);
        using var issuerScope = issuer.CreateScope();
        var user = await TokenTestHost.AddUserAsync(issuerScope.ServiceProvider, Email);
        var token = await issuerScope.ServiceProvider.GetRequiredService<IAccountTokenService>()
            .CreateEmailConfirmationAsync(Email, CancellationToken.None);
        Assert.NotNull(token);

        // The checker shares the user store; "another-key-ring" is a second host with its own ephemeral keys.
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

        Assert.False(await tokens.ConfirmEmailAsync(userId, code, CancellationToken.None));
        Assert.False(user.EmailConfirmed);
    }
}
