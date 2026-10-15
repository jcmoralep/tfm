using System.Net.Mail;
using RutaProducto.Application.Abstractions.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Email;

/// <summary>Delivers one queued message (the SMTP conversation). Separate from <see cref="IEmailSender"/>, which only enqueues.</summary>
internal interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Single reader of the email queue. A failed send is logged and skipped (no retry, the person can ask again) and never
/// stops the loop or the host. On shutdown the in-flight send is cancelled and queued messages are lost. Log lines carry the
/// kind, the exception type and the SMTP status code, never the address, subject, body or link.
/// </summary>
internal sealed class EmailDispatcher(EmailQueue queue, IEmailTransport transport, ILogger<EmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await transport.SendAsync(message, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // The exception object itself is not logged: its message can quote the recipient address.
                    logger.LogError(
                        "Email {Kind} failed: {ExceptionType} (SmtpStatusCode {SmtpStatusCode})",
                        message.Kind,
                        exception.GetType().Name,
                        (exception as SmtpException)?.StatusCode);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown while waiting for the next message.
        }
    }
}
