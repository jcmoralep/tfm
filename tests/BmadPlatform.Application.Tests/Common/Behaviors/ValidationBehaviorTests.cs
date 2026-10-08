using BmadPlatform.Application.Common.Behaviors;
using FluentValidation;
using MediatR;

namespace BmadPlatform.Application.Tests.Common.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Request_without_validators_reaches_the_next_step()
    {
        var behavior = new ValidationBehavior<Probe, string>([]);

        var response = await behavior.Handle(new Probe(string.Empty, 0), _ => Task.FromResult("next"), CancellationToken.None);

        Assert.Equal("next", response);
    }

    [Fact]
    public async Task Failures_from_every_validator_are_reported_together_and_next_is_not_called()
    {
        var behavior = new ValidationBehavior<Probe, string>([new NameValidator(), new AmountValidator()]);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new Probe(string.Empty, -1),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult("next");
            },
            CancellationToken.None));

        Assert.False(nextCalled);
        Assert.Equal(
            [nameof(Probe.Amount), nameof(Probe.Name)],
            exception.Errors.Select(failure => failure.PropertyName).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Valid_request_reaches_the_next_step()
    {
        var behavior = new ValidationBehavior<Probe, string>([new NameValidator(), new AmountValidator()]);

        var response = await behavior.Handle(new Probe("name", 1), _ => Task.FromResult("next"), CancellationToken.None);

        Assert.Equal("next", response);
    }

    public sealed record Probe(string Name, int Amount) : IRequest<string>;

    private sealed class NameValidator : AbstractValidator<Probe>
    {
        public NameValidator() => RuleFor(probe => probe.Name).NotEmpty();
    }

    private sealed class AmountValidator : AbstractValidator<Probe>
    {
        public AmountValidator() => RuleFor(probe => probe.Amount).GreaterThan(0);
    }
}
