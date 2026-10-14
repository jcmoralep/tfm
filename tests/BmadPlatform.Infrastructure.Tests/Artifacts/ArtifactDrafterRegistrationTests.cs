using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Infrastructure.Artifacts;
using BmadPlatform.Infrastructure.Assistant;
using BmadPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BmadPlatform.Infrastructure.Tests.Artifacts;

public sealed class ArtifactDrafterRegistrationTests
{
    [Fact]
    public void The_drafter_port_resolves_to_the_fake_as_a_singleton()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Never used to connect; it only satisfies the provider configuration.
                [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=bmadplatform_tests;User=tests",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IArtifactDrafter>();
        using var scope = provider.CreateScope();
        var second = scope.ServiceProvider.GetRequiredService<IArtifactDrafter>();

        Assert.IsType<FakeArtifactDrafter>(first);
        Assert.Same(first, second);
        Assert.IsType<FakeAssistantService>(provider.GetRequiredService<IAssistantService>());
    }
}
