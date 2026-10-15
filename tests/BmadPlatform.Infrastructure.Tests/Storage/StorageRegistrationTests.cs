using BmadPlatform.Application.Abstractions.Storage;
using BmadPlatform.Application.Features.Context;
using BmadPlatform.Infrastructure.Persistence;
using BmadPlatform.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BmadPlatform.Infrastructure.Tests.Storage;

public sealed class StorageRegistrationTests
{
    [Fact]
    public void Without_settings_local_storage_is_registered_as_a_singleton_with_the_default_cap()
    {
        using var provider = Build();

        var storage = provider.GetRequiredService<IFileStorage>();

        Assert.IsType<LocalFileStorage>(storage);
        Assert.Same(storage, provider.GetRequiredService<IFileStorage>());
        Assert.Equal(10L * 1024 * 1024, provider.GetRequiredService<ContextLimits>().MaxFileBytes);
    }

    [Fact]
    public void The_configured_size_becomes_the_per_file_cap()
    {
        using var provider = Build(("Storage:MaxFileSizeMb", "3"));

        Assert.Equal(3L * 1024 * 1024, provider.GetRequiredService<ContextLimits>().MaxFileBytes);
    }

    [Theory]
    [InlineData("S3")]
    [InlineData("Azure")]
    public void S3_and_unknown_providers_stop_the_startup_naming_the_setting(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Build(("Storage:Provider", value)));

        Assert.Contains("Storage:Provider", error.Message);
    }

    private static ServiceProvider Build(params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Never used to connect; it only satisfies the provider configuration.
                    [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=bmadplatform_tests;User=tests",
                }.Concat(values.Select(value => KeyValuePair.Create(value.Key, value.Value))))
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
