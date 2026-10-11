using BmadPlatform.Application.Features.Assistant.GetConversationAnswers;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Application.Features.Initiatives.MarkReadyToBuild;
using BmadPlatform.Application.Features.Initiatives.ReopenPlanning;
using BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using MediatR;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>
/// ISender for cross-module tests: routes the Initiatives and Assistant requests that other modules send to the real
/// handlers over the in-memory repositories, so the flow across modules is real. Anything else is a test mistake.
/// </summary>
public sealed class ModulesSender(
    InMemoryInitiativeRepository initiatives,
    InMemoryConversationRepository conversations,
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

        switch (request)
        {
            case GetInitiativeQuery query:
                var details = await new GetInitiativeQueryHandler(initiatives, currentUser).Handle(query, cancellationToken);

                return (TResponse)(object?)details!;
            case GetConversationAnswersQuery query:
                var answers = await new GetConversationAnswersQueryHandler(this, conversations, currentUser).Handle(query, cancellationToken);

                return (TResponse)(object?)answers!;
            default:
                throw new NotSupportedException($"{request.GetType().Name} is not routed by this double.");
        }
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        Record(request);

        switch (request)
        {
            case StartPlanningCommand command:
                await new StartPlanningCommandHandler(initiatives, currentUser, timeProvider).Handle(command, cancellationToken);
                break;
            case SetInitiativeDepthCommand command:
                await new SetInitiativeDepthCommandHandler(initiatives, currentUser, timeProvider).Handle(command, cancellationToken);
                break;
            case MarkReadyToBuildCommand command:
                await new MarkReadyToBuildCommandHandler(initiatives, currentUser, timeProvider).Handle(command, cancellationToken);
                break;
            case ReopenPlanningCommand command:
                await new ReopenPlanningCommandHandler(initiatives, currentUser, timeProvider).Handle(command, cancellationToken);
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
