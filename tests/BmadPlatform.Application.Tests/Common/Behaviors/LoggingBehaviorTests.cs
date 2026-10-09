using BmadPlatform.Application.Common.Behaviors;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Tests.TestDoubles;
using BmadPlatform.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.Common.Behaviors;

public sealed class LoggingBehaviorTests : IDisposable
{
    private const string SensitiveValue = "initiative-secret-4f2a";

    private readonly CapturingLoggerProvider logs = new();
    private readonly ILoggerFactory loggerFactory;

    public LoggingBehaviorTests()
    {
        loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
    }

    [Fact]
    public async Task Logs_request_name_and_duration_but_never_the_payload()
    {
        var behavior = CreateBehavior();
        var request = new SensitiveCommand(SensitiveValue, Description: $"Notes: {SensitiveValue}");

        await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.NotEmpty(logs.Entries);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain(SensitiveValue, entry.AllText));
        Assert.Contains(logs.Entries, entry => entry.Message.StartsWith("Handled SensitiveCommand in", StringComparison.Ordinal));
        Assert.Contains(
            logs.Entries,
            entry => entry.Properties.Any(property => property.Key == "ElapsedMilliseconds" && property.Value is double));
    }

    [Fact]
    public async Task Failure_is_logged_with_exception_type_only_and_rethrown()
    {
        var behavior = CreateBehavior();
        var request = new SensitiveCommand(SensitiveValue, Description: string.Empty);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            request,
            _ => throw new InvalidOperationException($"Handler echoed {SensitiveValue}"),
            CancellationToken.None));

        Assert.Contains(SensitiveValue, thrown.Message);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain(SensitiveValue, entry.AllText));

        var failure = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains(nameof(InvalidOperationException), failure.Message);
        Assert.Null(failure.Exception);
    }

    [Fact]
    public async Task Validation_failure_is_logged_as_warning_not_error()
    {
        var behavior = CreateBehavior();
        var request = new SensitiveCommand(SensitiveValue, Description: string.Empty);

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            request,
            _ => throw new ValidationException("Invalid input"),
            CancellationToken.None));

        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
        var failure = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(nameof(ValidationException), failure.Message);
    }

    [Theory]
    [MemberData(nameof(ExpectedFailures))]
    public async Task Not_found_and_domain_failures_are_logged_as_warning_not_error(Exception expected)
    {
        var behavior = CreateBehavior();
        var request = new SensitiveCommand(SensitiveValue, Description: string.Empty);

        await Assert.ThrowsAsync(expected.GetType(), () => behavior.Handle(
            request,
            _ => throw expected,
            CancellationToken.None));

        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
        var failure = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(expected.GetType().Name, failure.Message);
    }

    public static TheoryData<Exception> ExpectedFailures() =>
        new()
        {
            new NotFoundException("La iniciativa no existe."),
            new DomainException("Regla de dominio."),
            new ConflictException(),
            new AssistantUnavailableException(),
        };

    public void Dispose() => loggerFactory.Dispose();

    private LoggingBehavior<SensitiveCommand, string> CreateBehavior() =>
        new(loggerFactory.CreateLogger<LoggingBehavior<SensitiveCommand, string>>());

    public sealed record SensitiveCommand(string Secret, string Description) : IRequest<string>;
}
