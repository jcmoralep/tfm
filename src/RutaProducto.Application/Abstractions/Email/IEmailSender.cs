namespace RutaProducto.Application.Abstractions.Email;

/// <summary>The account emails the platform can send.</summary>
public enum EmailKind
{
    ConfirmEmail,
    AccountExists,
    ResetPassword,
}

/// <summary>
/// One plain-text message. <see cref="ToString"/> is the kind only, so a log line that prints the message
/// can never expose the address, the subject, the body or a link.
/// </summary>
public sealed record EmailMessage(string To, EmailKind Kind, string Subject, string Body)
{
    public override string ToString() => Kind.ToString();
}

/// <summary>
/// Request-side email seam. <see cref="Enqueue"/> never blocks on delivery and never throws, so the response time
/// does not depend on the mail server or on whether an address exists.
/// </summary>
public interface IEmailSender
{
    void Enqueue(EmailMessage message);
}

/// <summary>What the account flows need to know about email: whether it is on, whether sign-in needs a confirmed address, and the base for links.</summary>
public sealed record AccountEmailSettings(bool EmailEnabled, bool RequireConfirmedEmail, Uri? PublicBaseUrl);
