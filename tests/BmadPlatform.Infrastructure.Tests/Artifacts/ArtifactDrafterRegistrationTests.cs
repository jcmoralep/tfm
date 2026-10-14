using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;
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

    [Fact]
    public async Task With_the_fake_provider_the_registered_drafter_writes_the_stored_golden()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=bmadplatform_tests;User=tests",
                ["Assistant:Provider"] = "Fake",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        var answers = DraftSamples.Answers(
            InitiativeDepth.Small,
            new Dictionary<string, string>
            {
                [AssistantScript.Keys.Idea] = "Que el equipo de ventas consulte el estado de un pedido sin llamar a bodega.",
                [AssistantScript.Keys.Users] = "Las vendedoras de las tiendas\nEl personal de bodega",
                [AssistantScript.Keys.Problem] = "Hoy cada consulta toma diez minutos por teléfono y los clientes se quejan de la demora.",
                [AssistantScript.Keys.Success] = "Que ninguna vendedora tenga que llamar a bodega\nQue bajen las quejas por demoras",
                [AssistantScript.Keys.Capabilities] = "Buscar un pedido por su número\n- Ver en qué estado está\n3. Recibir un aviso cuando cambie",
            },
            [AssistantScript.Keys.OutOfScope]);

        var markdown = await provider.GetRequiredService<IArtifactDrafter>()
            .DraftAsync(DraftSamples.Request(ArtifactType.Spec, InitiativeDepth.Small, answers), default);

        var golden = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(ThisFile())!, "Goldens", "spec-small.md"));
        Assert.Equal(golden.TrimEnd('\n'), markdown);
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
}
