using System.Net;
using System.Net.Mail;
using System.Text;
using RutaProducto.Application.Abstractions.Email;

namespace RutaProducto.Infrastructure.Email;

/// <summary>
/// Sends one plain-text UTF-8 message per SMTP connection with <c>System.Net.Mail</c> (STARTTLS on 587; implicit TLS is not
/// supported). A new client per message keeps the class free of shared state. The MailKit or HTTP-API alternative is a
/// future implementation of <see cref="IEmailTransport"/>.
/// </summary>
internal sealed class SmtpEmailTransport(SmtpOptions options) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        // SmtpClient is documented as "not recommended" but is the only SMTP client in the framework; adding a package was
        // ruled out for the MVP. On this SDK it raises no build warning, so no suppression is needed.
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            Timeout = options.TimeoutSeconds * 1000,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (options.Username is not null)
        {
            client.Credentials = new NetworkCredential(options.Username, options.Password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName, Encoding.UTF8),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
        };
        mail.To.Add(message.To);

        await client.SendMailAsync(mail, cancellationToken);
    }
}
