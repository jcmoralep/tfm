namespace RutaProducto.Application.Abstractions.Authentication;

/// <summary>A user id and an opaque, URL-safe code that proves control of the mailbox. The code never reaches a log.</summary>
public sealed record AccountToken(string UserId, string Code)
{
    // Records print every property by default; a log line must never expose the code.
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
}

/// <summary>Builds the absolute links placed in emails, always from the configured public base URL and never from the request host.</summary>
public interface IAccountLinks
{
    Uri EmailConfirmation(AccountToken token);

    Uri Login();

    Uri ForgotPassword();
}
