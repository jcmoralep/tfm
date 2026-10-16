using RutaProducto.Infrastructure.DataProtection;
using RutaProducto.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FrameworkDataProtectionOptions = Microsoft.AspNetCore.DataProtection.DataProtectionOptions;

namespace RutaProducto.Infrastructure.Tests.DataProtection;

public sealed class DataProtectionOptionsTests
{
    [Theory]
    [InlineData(null, KeyStoreProvider.Default)]
    [InlineData("", KeyStoreProvider.Default)]
    [InlineData("default", KeyStoreProvider.Default)]
    [InlineData("Database", KeyStoreProvider.Database)]
    [InlineData(" database ", KeyStoreProvider.Database)]
    public void The_provider_is_the_default_unless_the_database_is_asked_for(string? value, KeyStoreProvider expected)
    {
        Assert.Equal(expected, DataProtectionOptions.BindAndValidate(Config(value)).Provider);
    }

    [Theory]
    [InlineData("redis-secret")]
    [InlineData("42")]
    public void An_unknown_provider_lists_the_valid_values_and_never_echoes_the_value(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => DataProtectionOptions.BindAndValidate(Config(value)));

        Assert.Contains("DataProtection:Provider", error.Message);
        Assert.Contains("Default", error.Message);
        Assert.Contains("Database", error.Message);
        Assert.DoesNotContain(value, error.Message);
    }

    [Fact]
    public void Registration_follows_the_provider()
    {
        using var byDefault = Build(null);
        using var inDatabase = Build("Database");

        Assert.Null(byDefault.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
        Assert.Null(byDefault.GetRequiredService<IOptions<FrameworkDataProtectionOptions>>().Value.ApplicationDiscriminator);

        Assert.NotNull(inDatabase.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
        Assert.Equal(
            KeyPersistenceServiceCollectionExtensions.ApplicationName,
            inDatabase.GetRequiredService<IOptions<FrameworkDataProtectionOptions>>().Value.ApplicationDiscriminator);
    }

    private static IConfiguration Config(string? value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([KeyValuePair.Create<string, string?>("DataProtection:Provider", value)])
            .Build();

    private static ServiceProvider Build(string? provider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Never used to connect; it only satisfies the provider configuration.
                [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=rutaproducto_tests;User=tests",
                ["DataProtection:Provider"] = provider,
            })
            .Build());

        return services.BuildServiceProvider();
    }
}
