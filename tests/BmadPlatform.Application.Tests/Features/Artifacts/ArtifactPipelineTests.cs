using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Artifacts.ApproveArtifact;
using BmadPlatform.Application.Features.Artifacts.GenerateArtifact;
using BmadPlatform.Application.Features.Artifacts.GetArtifact;
using BmadPlatform.Application.Features.Artifacts.GetArtifactsOverview;
using BmadPlatform.Application.Features.Artifacts.RegenerateArtifact;
using BmadPlatform.Application.Features.Artifacts.SaveArtifactContent;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Application.Tests.TestDoubles;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

/// <summary>Runs the document use cases through the real MediatR pipeline: wiring, validation and logging.</summary>
public sealed class ArtifactPipelineTests : IDisposable
{
    private const string DistinctiveText = "texto-distintivo-5b2d";

    private readonly CapturingLoggerProvider logs = new();
    private readonly ArtifactTestContext context = new();
    private readonly ServiceProvider provider;

    public ArtifactPipelineTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders().AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        services.AddApplication();
        services.AddSingleton<IInitiativeRepository>(context.Initiatives.Repository);
        services.AddSingleton<IConversationRepository>(context.Assistant.Conversations);
        services.AddSingleton<IArtifactRepository>(context.Artifacts);
        services.AddSingleton<IArtifactDrafter>(context.Drafter);
        services.AddSingleton<ICurrentUser>(context.User);
        services.AddSingleton<TimeProvider>(context.Clock);
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    [Fact]
    public async Task The_whole_flow_runs_through_the_registered_handlers_and_ends_ready_to_build()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var generated = await mediator.Send(new GenerateArtifactCommand(id, ArtifactType.Spec));
        var saved = await mediator.Send(new SaveArtifactContentCommand(id, ArtifactType.Spec, generated.Version, "# Editada"));
        var regenerated = await mediator.Send(new RegenerateArtifactCommand(id, ArtifactType.Spec, saved.Version, true));
        var approved = await mediator.Send(new ApproveArtifactCommand(id, ArtifactType.Spec, regenerated.Version));
        var read = await mediator.Send(new GetArtifactQuery(id, ArtifactType.Spec));
        var overview = await mediator.Send(new GetArtifactsOverviewQuery(id));

        Assert.Equal((3, ArtifactStatus.Approved), (approved.Version, approved.Status));
        Assert.Equal(approved.Content, read!.Content);
        Assert.Equal(ArtifactStatus.Approved, overview!.Slots.Single().Status);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
    }

    [Fact]
    public async Task Content_over_the_cap_is_rejected_by_the_pipeline_before_the_handler()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new GenerateArtifactCommand(id, ArtifactType.Spec));
        var saves = context.Artifacts.SaveCount;

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => mediator.Send(new SaveArtifactContentCommand(id, ArtifactType.Spec, 1, new string('x', 20001))));

        Assert.Equal("El documento no puede superar los 20000 caracteres.", Assert.Single(error.Errors).ErrorMessage);
        Assert.Equal(saves, context.Artifacts.SaveCount);
    }

    [Fact]
    public async Task Document_text_never_reaches_the_logs_on_success_or_on_failure()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new GenerateArtifactCommand(id, ArtifactType.Spec));

        await mediator.Send(new SaveArtifactContentCommand(id, ArtifactType.Spec, 1, DistinctiveText));
        await Assert.ThrowsAsync<ConflictException>(
            () => mediator.Send(new SaveArtifactContentCommand(id, ArtifactType.Spec, 1, DistinctiveText + " otra")));

        Assert.Equal(DistinctiveText, context.StoredArtifact(id, ArtifactType.Spec).Content);
        Assert.NotEmpty(logs.Entries);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain(DistinctiveText, entry.AllText, StringComparison.Ordinal));
        Assert.Contains(logs.Entries, entry => entry.Message.StartsWith("Handled SaveArtifactContentCommand in", StringComparison.Ordinal));
    }

    [Fact]
    public void Commands_and_results_that_carry_the_document_redact_it_in_their_string_form()
    {
        var command = new SaveArtifactContentCommand(Guid.NewGuid(), ArtifactType.Spec, 2, DistinctiveText);
        var view = new ArtifactView(
            Guid.NewGuid(), "App", InitiativeStatus.Planning, InitiativeDepth.Small, ArtifactType.Spec, true,
            ArtifactAvailability.Available, 2, ArtifactStatus.Draft, DistinctiveText, null, null, [], false, false, false, false, false, false, [], []);

        Assert.DoesNotContain(DistinctiveText, command.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(DistinctiveText, view.ToString(), StringComparison.Ordinal);
        Assert.Contains("ExpectedVersion = 2", command.ToString(), StringComparison.Ordinal);
    }

    public void Dispose() => provider.Dispose();
}
