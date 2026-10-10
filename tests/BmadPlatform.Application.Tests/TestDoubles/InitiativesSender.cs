using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using MediatR;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>
/// ISender that routes the three Initiatives requests the assistant uses to the real handlers over the in-memory
/// initiative repository, so the cross-module flow is real. Anything else is a test mistake.
/// </summary>
public sealed class InitiativesSender(
    InMemoryInitiativeRepository repository,
    FakeCurrentUser currentUser,
    TimeProvider timeProvider) : ISender
{
    private readonly List<object> sent = [];

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<object> Sent => sent;

    /// <summary>Runs before each request is handled; may throw to simulate a failure.</summary>
    public Action<object>? BeforeHandle { get; set; }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Record(request);

        if (request is GetInitiativeQuery query)
        {
            var details = await new GetInitiativeQueryHandler(repository, currentUser).Handle(query, cancellationToken);

            return (TResponse)(object?)details!;
        }

        throw new NotSupportedException($"{request.GetType().Name} is not routed by this double.");
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        Record(request);

        switch (request)
        {
            case StartPlanningCommand command:
                await new StartPlanningCommandHandler(repository, currentUser, timeProvider).Handle(command, cancellationToken);
                break;
            case SetInitiativeDepthCommand command:
                await new SetInitiativeDepthCommandHandler(repository, currentUser, timeProvider).Handle(command, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"{request.GetType().Name} is not routed by this double.");
        }
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    private void Record(object request)
    {
        BeforeHandle?.Invoke(request);
        sent.Add(request);
    }
}
