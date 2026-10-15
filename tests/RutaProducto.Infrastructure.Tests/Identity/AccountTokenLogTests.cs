using RutaProducto.Application.Abstractions.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Tests.Identity;

public sealed class AccountTokenLogTests
{
    private const string Email = "ana@example.com";
    private const string NewPassword = "Brand-New-456";

    [Fact]
    public async Task The_token_flows_log_no_address_code_or_password()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = TokenTestHost.Build(new InMemoryUserStore(), logs: logs);
        using var scope = provider.CreateScope();
        var user = await TokenTestHost.AddUserAsync(scope.ServiceProvider, Email);
        var tokens = scope.ServiceProvider.GetRequiredService<IAccountTokenService>();

        var confirmation = await tokens.CreateEmailConfirmationAsync(Email, CancellationToken.None);
        await tokens.ConfirmEmailAsync(user.Id, confirmation!.Code, CancellationToken.None);
        await tokens.ConfirmEmailAsync(user.Id, "garbage", CancellationToken.None);
        var reset = await tokens.CreatePasswordResetAsync(Email, CancellationToken.None);
        await tokens.ResetPasswordAsync(user.Id, reset!.Code, "abc", CancellationToken.None);
        await tokens.ResetPasswordAsync(user.Id, reset.Code, NewPassword, CancellationToken.None);
        await tokens.ResetPasswordAsync(user.Id, "garbage", NewPassword, CancellationToken.None);

        var lines = logs.Lines.Where(line => line.Category.EndsWith("IdentityAccountTokenService", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain(Email, line.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(confirmation.Code, line.Text);
            Assert.DoesNotContain(reset.Code, line.Text);
            Assert.DoesNotContain("Secret-123", line.Text);
            Assert.DoesNotContain(NewPassword, line.Text);
        });
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(string Category, string Text)> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, Lines);

        public void Dispose()
        {
        }

        private sealed class Capture(string category, List<(string Category, string Text)> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Add((category, formatter(state, exception) + exception));
        }
    }
}
