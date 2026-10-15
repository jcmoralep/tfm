using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Infrastructure.Email;
using RutaProducto.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RutaProducto.Infrastructure.Tests.Email;

public sealed class EmailRegistrationTests
{
    [Fact]
    public void Without_configuration_the_sender_discards_and_no_worker_is_registered()
    {
        using var provider = Build();

        Assert.IsType<DisabledEmailSender>(provider.GetRequiredService<IEmailSender>());
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), service => service is EmailDispatcher);
        Assert.Equal(new AccountEmailSettings(false, false, null), provider.GetRequiredService<AccountEmailSettings>());
    }

    [Fact]
    public void With_smtp_the_queued_sender_and_the_dispatcher_are_registered()
    {
        using var provider = Build(
            ("Email:Provider", "Smtp"),
            ("Email:PublicBaseUrl", "https://app.example.com"),
            ("Smtp:Host", "smtp.example.com"),
            ("Smtp:From", "no-reply@example.com"));

        Assert.IsType<QueuedEmailSender>(provider.GetRequiredService<IEmailSender>());
        Assert.IsType<SmtpEmailTransport>(provider.GetRequiredService<IEmailTransport>());
        Assert.Contains(provider.GetServices<IHostedService>(), service => service is EmailDispatcher);
        Assert.True(provider.GetRequiredService<AccountEmailSettings>().EmailEnabled);
    }

    private static ServiceProvider Build(params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Never used to connect; it only satisfies the provider configuration.
                    [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=rutaproducto_tests;User=tests",
                }.Concat(values.Select(value => KeyValuePair.Create(value.Key, value.Value))))
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
