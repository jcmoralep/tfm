using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Storage;
using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Features.Artifacts.ApproveArtifact;
using RutaProducto.Application.Features.Artifacts.AssignQuestionOwners;
using RutaProducto.Application.Features.Artifacts.GenerateArtifact;
using RutaProducto.Application.Features.Artifacts.GetArtifact;
using RutaProducto.Application.Features.Artifacts.GetArtifactsOverview;
using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Application.Features.Artifacts.RegenerateArtifact;
using RutaProducto.Application.Features.Artifacts.SaveArtifactContent;
using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Context;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Initiatives;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Tests.Features.Artifacts;

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
        services.AddSingleton<IAttachmentRepository>(context.Assistant.Attachments);
        services.AddSingleton<IFileStorage>(context.Assistant.Storage);
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
    public async Task Owner_and_revisit_text_never_reaches_the_logs_the_validation_errors_or_the_string_form()
    {
        const string Owner = "dueno-distintivo-7c1e";
        const string Revisit = "revision-distintiva-9a3f";
        var id = await context.AtClosing(InitiativeDepth.Small);
        context.Drafter.Output = "# Spec\n- **Q-1**: uno (Responsable: por asignar; Revisar: por definir)";
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new GenerateArtifactCommand(id, ArtifactType.Spec));
        var assignment = new QuestionAssignment("Q-1", Owner, Revisit);

        var saved = await mediator.Send(new AssignQuestionOwnersCommand(id, ArtifactType.Spec, 1, [assignment]));
        await Assert.ThrowsAsync<ConflictException>(() => mediator.Send(new AssignQuestionOwnersCommand(id, ArtifactType.Spec, 1, [assignment])));
        var invalid = await Assert.ThrowsAsync<ValidationException>(
            () => mediator.Send(new AssignQuestionOwnersCommand(id, ArtifactType.Spec, 2, [assignment with { Owner = Owner + "(" }])));

        Assert.Contains($"(Responsable: {Owner}; Revisar: {Revisit})", saved.Content, StringComparison.Ordinal);
        Assert.NotEmpty(logs.Entries);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain("distintiv", entry.AllText, StringComparison.Ordinal));
        Assert.Contains(logs.Entries, entry => entry.Message.StartsWith("Handled AssignQuestionOwnersCommand in", StringComparison.Ordinal));
        Assert.DoesNotContain("distintiv", invalid.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("distintiv", new AssignQuestionOwnersCommand(id, ArtifactType.Spec, 1, [assignment]).ToString(), StringComparison.Ordinal);
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
