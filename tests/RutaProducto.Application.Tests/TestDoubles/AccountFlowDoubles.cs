using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Email;

namespace RutaProducto.Application.Tests.TestDoubles;

/// <summary>Keeps what was enqueued; nothing is sent.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Messages { get; } = [];

    public void Enqueue(EmailMessage message) => Messages.Add(message);
}

/// <summary>Configurable token results with a call log. <see cref="KnownEmails"/> are addresses that have a pending confirmation.</summary>
public sealed class FakeAccountTokenService : IAccountTokenService
{
    public HashSet<string> KnownEmails { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ConfirmResult { get; set; } = true;

    public List<string> Calls { get; } = [];

    /// <summary>
    /// Typed address to the address stored on the account, for when the database collation matches them despite a
    /// difference (an accent). Addresses not listed here are stored exactly as typed.
    /// </summary>
    public Dictionary<string, string> StoredAddresses { get; } = new(StringComparer.OrdinalIgnoreCase);

    private string Stored(string typed) => StoredAddresses.TryGetValue(typed, out var stored) ? stored : typed;

    public Task<AccountToken?> CreateEmailConfirmationAsync(string email, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(CreateEmailConfirmationAsync));

        return Task.FromResult(KnownEmails.Contains(email) ? new AccountToken("user-1", "code-1", Stored(email)) : null);
    }

    public Task<string?> FindStoredEmailAsync(string email, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(FindStoredEmailAsync));

        var exists = KnownEmails.Contains(email) || ExistingEmails.Contains(email);

        return Task.FromResult(exists ? Stored(email) : null);
    }

    public Task<bool> ConfirmEmailAsync(string userId, string code, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ConfirmEmailAsync));

        return Task.FromResult(ConfirmResult);
    }

    /// <summary>Addresses that have an account (any state); they get a reset token.</summary>
    public HashSet<string> ExistingEmails { get; } = new(StringComparer.OrdinalIgnoreCase);

    public PasswordResetResult ResetResult { get; set; } = PasswordResetResult.Succeeded;

    public Task<AccountToken?> CreatePasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(CreatePasswordResetAsync));

        return Task.FromResult(ExistingEmails.Contains(email) ? new AccountToken("user-1", "reset-code-1", Stored(email)) : null);
    }

    public Task<PasswordResetResult> ResetPasswordAsync(string userId, string code, string newPassword, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ResetPasswordAsync));

        return Task.FromResult(ResetResult);
    }
}

/// <summary>Fixed, recognisable links.</summary>
public sealed class FakeAccountLinks : IAccountLinks
{
    public Uri EmailConfirmation(AccountToken token) => new($"https://app.test/confirmar-correo?usuario={token.UserId}&codigo={token.Code}");

    public Uri PasswordReset(AccountToken token) => new($"https://app.test/restablecer-contrasena?usuario={token.UserId}&codigo={token.Code}");

    public Uri Login() => new("https://app.test/login");

    public Uri ForgotPassword() => new("https://app.test/recuperar-contrasena");
}
