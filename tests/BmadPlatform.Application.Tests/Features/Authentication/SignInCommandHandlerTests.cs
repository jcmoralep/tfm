using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Authentication.SignIn;

namespace BmadPlatform.Application.Tests.Features.Authentication;

public sealed class SignInCommandHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handler_trims_the_email_and_forwards_the_remember_me_choice(bool rememberMe)
    {
        var authService = new RecordingAuthService();
        var handler = new SignInCommandHandler(authService);

        var result = await handler.Handle(
            new SignInCommand("  ana@example.com ", "secret", rememberMe),
            CancellationToken.None);

        Assert.Equal(AuthenticationResult.Succeeded, result);
        Assert.Equal("ana@example.com", authService.Email);
        Assert.Equal(rememberMe, authService.RememberMe);
    }

    [Fact]
    public void Remember_me_is_off_by_default()
    {
        Assert.False(new SignInCommand("ana@example.com", "secret").RememberMe);
    }

    private sealed class RecordingAuthService : IAuthService
    {
        public string? Email { get; private set; }

        public bool RememberMe { get; private set; }

        public Task<AuthenticationResult> SignInAsync(
            string email,
            string password,
            bool rememberMe,
            CancellationToken cancellationToken = default)
        {
            Email = email;
            RememberMe = rememberMe;
            return Task.FromResult(AuthenticationResult.Succeeded);
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
