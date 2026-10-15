namespace RutaProducto.Application.Abstractions.Authentication;

/// <summary>
/// Extension point for authentication. The current implementation uses ASP.NET Core Identity
/// with email and password; SSO or role-aware implementations can replace it later.
/// </summary>
public interface IAuthService
{
    Task<AuthenticationResult> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default);

    /// <summary>Creates an account. It does not sign the person in.</summary>
    Task<RegistrationResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

public enum AuthenticationResult
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
    NotAllowed,
}

public enum RegistrationStatus
{
    Succeeded,
    EmailTaken,
    InvalidPassword,
    CaptchaRejected,
    CaptchaUnavailable,
}

/// <param name="Status">Outcome of the registration.</param>
/// <param name="PasswordErrors">Spanish messages for the password rules that failed; empty unless the status is InvalidPassword.</param>
public sealed record RegistrationResult(RegistrationStatus Status, IReadOnlyList<string> PasswordErrors)
{
    public static RegistrationResult Succeeded { get; } = new(RegistrationStatus.Succeeded, []);

    public static RegistrationResult EmailTaken { get; } = new(RegistrationStatus.EmailTaken, []);

    public static RegistrationResult CaptchaRejected { get; } = new(RegistrationStatus.CaptchaRejected, []);

    public static RegistrationResult CaptchaUnavailable { get; } = new(RegistrationStatus.CaptchaUnavailable, []);

    public static RegistrationResult InvalidPassword(IReadOnlyList<string> errors) => new(RegistrationStatus.InvalidPassword, errors);
}
