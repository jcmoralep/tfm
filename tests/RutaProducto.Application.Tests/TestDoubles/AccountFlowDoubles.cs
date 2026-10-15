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

    public Task<AccountToken?> CreateEmailConfirmationAsync(string email, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(CreateEmailConfirmationAsync));

        return Task.FromResult(KnownEmails.Contains(email) ? new AccountToken("user-1", "code-1") : null);
    }

    public Task<bool> ConfirmEmailAsync(string userId, string code, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ConfirmEmailAsync));

        return Task.FromResult(ConfirmResult);
    }
}

/// <summary>Fixed, recognisable links.</summary>
public sealed class FakeAccountLinks : IAccountLinks
{
    public Uri EmailConfirmation(AccountToken token) => new($"https://app.test/confirmar-correo?usuario={token.UserId}&codigo={token.Code}");

    public Uri Login() => new("https://app.test/login");

    public Uri ForgotPassword() => new("https://app.test/recuperar-contrasena");
}
