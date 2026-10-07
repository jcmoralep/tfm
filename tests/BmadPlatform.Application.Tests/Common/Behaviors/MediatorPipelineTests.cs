using BmadPlatform.Application.Common.Behaviors;
using BmadPlatform.Application.Tests.TestDoubles;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.Common.Behaviors;

/// <summary>
/// Exercises the real pipeline registered by <see cref="DependencyInjection.AddApplication"/>.
/// </summary>
public sealed class MediatorPipelineTests : IDisposable
{
    private readonly CapturingLoggerProvider logs = new();
    private readonly ProbeHandler handler = new();
    private readonly ServiceProvider provider;

    public MediatorPipelineTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders().AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        services.AddApplication();
        services.AddSingleton<IRequestHandler<ProbeRequest, string>>(handler);
        services.AddTransient<IValidator<ProbeRequest>, ProbeRequestValidator>();
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void Behaviors_are_registered_in_order_logging_then_validation()
    {
        using var scope = provider.CreateScope();

        var behaviors = scope.ServiceProvider
            .GetServices<IPipelineBehavior<ProbeRequest, string>>()
            .Select(behavior => behavior.GetType())
            .ToArray();

        Assert.Equal(
            [typeof(LoggingBehavior<ProbeRequest, string>), typeof(ValidationBehavior<ProbeRequest, string>)],
            behaviors);
    }

    [Fact]
    public async Task Valid_request_flows_through_logging_and_reaches_the_handler()
    {
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new ProbeRequest("valid"));

        Assert.Equal("handled:valid", response);
        Assert.Equal(1, handler.Calls);

        var messages = PipelineLogMessages();
        Assert.Equal(2, messages.Length);
        Assert.StartsWith("Handling ProbeRequest", messages[0]);
        Assert.StartsWith("Handled ProbeRequest in", messages[1]);
    }

    [Fact]
    public async Task Invalid_request_is_rejected_before_the_handler_and_the_failure_is_logged()
    {
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(new ProbeRequest(string.Empty)));

        Assert.Single(exception.Errors);
        Assert.Equal(0, handler.Calls);

        // Logging wraps validation: it sees the request start and the validation failure.
        var messages = PipelineLogMessages();
        Assert.Equal(2, messages.Length);
        Assert.StartsWith("Handling ProbeRequest", messages[0]);
        Assert.Contains("ProbeRequest failed after", messages[1]);
        Assert.Contains(nameof(ValidationException), messages[1]);
    }

    public void Dispose() => provider.Dispose();

    private string[] PipelineLogMessages() =>
        logs.Entries
            .Where(entry => entry.Category.StartsWith(typeof(LoggingBehavior<,>).Namespace!, StringComparison.Ordinal))
            .Select(entry => entry.Message)
            .ToArray();

    public sealed record ProbeRequest(string Value) : IRequest<string>;

    public sealed class ProbeRequestValidator : AbstractValidator<ProbeRequest>
    {
        public ProbeRequestValidator() => RuleFor(request => request.Value).NotEmpty();
    }

    public sealed class ProbeHandler : IRequestHandler<ProbeRequest, string>
    {
        public int Calls { get; private set; }

        public Task<string> Handle(ProbeRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult($"handled:{request.Value}");
        }
    }
}
