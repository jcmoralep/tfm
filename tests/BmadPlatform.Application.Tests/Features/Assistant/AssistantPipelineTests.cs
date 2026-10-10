using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.SendMessage;
using BmadPlatform.Application.Features.Assistant.StartConversation;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Application.Tests.Features.Initiatives;
using BmadPlatform.Application.Tests.TestDoubles;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.Features.Assistant;

/// <summary>Runs the assistant use cases through the real MediatR pipeline: wiring, validation and logging.</summary>
public sealed class AssistantPipelineTests : IDisposable
{
    private const string DistinctiveText = "texto-distintivo-7e3c";

    private readonly CapturingLoggerProvider logs = new();
    private readonly InitiativeTestContext initiatives = new();
    private readonly InMemoryConversationRepository conversations = new();
    private readonly ServiceProvider provider;

    public AssistantPipelineTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders().AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        services.AddApplication();
        services.AddSingleton<IInitiativeRepository>(initiatives.Repository);
        services.AddSingleton<IConversationRepository>(conversations);
        services.AddSingleton<IAssistantService, ScriptedAssistantService>();
        services.AddSingleton<ICurrentUser>(initiatives.User);
        services.AddSingleton<TimeProvider>(initiatives.Clock);
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    [Fact]
    public async Task A_send_with_a_distinctive_text_logs_no_message_content()
    {
        var id = await initiatives.CreateClarifying("App");
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var opened = await mediator.Send(new StartConversationCommand(id));

        await mediator.Send(new SendMessageCommand(id, opened.Version, DistinctiveText, null));

        Assert.Contains(conversations.Stored.Single().Messages, m => m.Content == DistinctiveText);
        Assert.NotEmpty(logs.Entries);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain(DistinctiveText, entry.AllText, StringComparison.Ordinal));
        Assert.Contains(logs.Entries, entry => entry.Message.StartsWith("Handled SendMessageCommand in", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_over_long_answer_is_rejected_by_the_pipeline_before_the_handler_and_is_not_logged()
    {
        var id = await initiatives.CreateClarifying("App");
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var opened = await mediator.Send(new StartConversationCommand(id));
        var saves = conversations.SaveCount;

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => mediator.Send(new SendMessageCommand(id, opened.Version, new string('x', 2001), null)));

        Assert.Equal("La respuesta no puede superar los 2000 caracteres.", Assert.Single(error.Errors).ErrorMessage);
        Assert.Equal(saves, conversations.SaveCount);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain(new string('x', 50), entry.AllText, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failure_in_the_pipeline_is_logged_as_a_warning_for_conflicts()
    {
        var id = await initiatives.CreateClarifying("App");
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new StartConversationCommand(id));

        await Assert.ThrowsAsync<ConflictException>(
            () => mediator.Send(new SendMessageCommand(id, 999, DistinctiveText, null)));

        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("ConflictException", StringComparison.Ordinal));
        Assert.All(logs.Entries, entry => Assert.DoesNotContain(DistinctiveText, entry.AllText, StringComparison.Ordinal));
    }

    public void Dispose() => provider.Dispose();
}
