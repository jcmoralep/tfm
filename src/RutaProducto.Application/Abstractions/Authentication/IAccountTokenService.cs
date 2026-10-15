namespace RutaProducto.Application.Abstractions.Authentication;

/// <summary>
/// A user id, an opaque URL-safe code that proves control of the mailbox, and the address STORED on the account.
/// Mail always goes to <paramref name="Email"/>, never to the address typed in the request: the database collation can
/// match a typed address that differs from the stored one. Neither the code nor the address reaches a log.
/// </summary>
public sealed record AccountToken(string UserId, string Code, string Email)
{
    // Records print every property by default; a log line must never expose the code or the address.
    public override string ToString() => nameof(AccountToken);
}

/// <summary>
/// Creates and checks the single-use tokens of the email flows. Implemented over ASP.NET Core Identity so Application
/// never sees data protection or encoding details.
/// </summary>
public interface IAccountTokenService
{
    /// <summary>Returns null when the address is unknown or already confirmed.</summary>
    Task<AccountToken?> CreateEmailConfirmationAsync(string email, CancellationToken cancellationToken);

    /// <summary>True only for a valid, current token. Malformed, altered, expired and unknown-user cases are all just false.</summary>
    Task<bool> ConfirmEmailAsync(string userId, string code, CancellationToken cancellationToken);

    /// <summary>Returns null only when the address is unknown. Any existing account gets a token: the mailbox is the proof.</summary>
    Task<AccountToken?> CreatePasswordResetAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the new password when the token is valid. Success also confirms the address and does not sign anyone in.
    /// Every token problem is the same <see cref="PasswordResetStatus.InvalidLink"/>.
    /// </summary>
    Task<PasswordResetResult> ResetPasswordAsync(string userId, string code, string newPassword, CancellationToken cancellationToken);

    /// <summary>The address stored on the account that matches <paramref name="email"/>, or null when there is none.</summary>
    Task<string?> FindStoredEmailAsync(string email, CancellationToken cancellationToken);
}

public enum PasswordResetStatus
{
    Succeeded,
    InvalidLink,
    InvalidPassword,
}

/// <param name="Status">Outcome of the reset.</param>
/// <param name="PasswordErrors">Spanish messages for the password rules that failed; empty unless the status is InvalidPassword.</param>
public sealed record PasswordResetResult(PasswordResetStatus Status, IReadOnlyList<string> PasswordErrors)
{
    public static PasswordResetResult Succeeded { get; } = new(PasswordResetStatus.Succeeded, []);

    public static PasswordResetResult InvalidLink { get; } = new(PasswordResetStatus.InvalidLink, []);

    public static PasswordResetResult InvalidPassword(IReadOnlyList<string> errors) => new(PasswordResetStatus.InvalidPassword, errors);
}

/// <summary>Builds the absolute links placed in emails, always from the configured public base URL and never from the request host.</summary>
public interface IAccountLinks
{
    Uri EmailConfirmation(AccountToken token);

    Uri PasswordReset(AccountToken token);

    Uri Login();

    Uri ForgotPassword();
}
