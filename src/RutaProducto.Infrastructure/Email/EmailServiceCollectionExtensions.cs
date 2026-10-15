using RutaProducto.Application.Abstractions.Email;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers the sender selected by <c>Email:Provider</c>. With <c>None</c> the sender discards messages and no worker
    /// runs. With <c>Smtp</c> the queue, the SMTP transport and the dispatcher (hosted service) are added.
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, EmailOptions options)
    {
        services.AddSingleton(options.ToSettings());

        if (options.Provider == EmailProvider.Smtp && options.Smtp is not null)
        {
            services.AddSingleton(options.Smtp);
            services.AddSingleton(new EmailQueue());
            services.AddSingleton<IEmailSender, QueuedEmailSender>();
            services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
            services.AddHostedService<EmailDispatcher>();
        }
        else
        {
            services.AddSingleton<IEmailSender, DisabledEmailSender>();
        }

        return services;
    }
}
