using BmadPlatform.Application.Features.Assistant.GetConversationAnswers;
using BmadPlatform.Application.Features.Context;
using BmadPlatform.Application.Features.Context.AddFileAttachment;
using BmadPlatform.Application.Features.Context.AddLinkAttachment;
using BmadPlatform.Application.Features.Context.AddNoteAttachment;
using BmadPlatform.Application.Features.Context.GetContext;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Application.Features.Initiatives.MarkReadyToBuild;
using BmadPlatform.Application.Features.Initiatives.ReopenPlanning;
using BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>
/// ISender for cross-module tests: routes the Initiatives and Assistant requests that other modules send to the real
/// handlers over the in-memory repositories, so the flow across modules is real. Anything else is a test mistake.
/// </summary>
public sealed class ModulesSender(
    InMemoryInitiativeRepository initiatives,
    InMemoryConversationRepository conversations,
    FakeCurrentUser currentUser,
    TimeProvider timeProvider,
    ContextRoutes? context = null) : ISender
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
            case GetContextQuery query when context is not null:
                var view = await new GetContextQueryHandler(this, context.Attachments, currentUser, context.Limits).Handle(query, cancellationToken);

                return (TResponse)(object?)view!;
            case AddFileAttachmentCommand command when context is not null:
                var file = await new AddFileAttachmentCommandHandler(
                    this, context.Attachments, context.Storage, currentUser, timeProvider, context.Limits,
                    context.Loggers.CreateLogger<AddFileAttachmentCommandHandler>()).Handle(command, cancellationToken);

                return (TResponse)(object)file;
            case AddLinkAttachmentCommand command when context is not null:
                var link = await new AddLinkAttachmentCommandHandler(
                    this, context.Attachments, currentUser, timeProvider, context.Limits).Handle(command, cancellationToken);

                return (TResponse)(object)link;
            case AddNoteAttachmentCommand command when context is not null:
                var note = await new AddNoteAttachmentCommandHandler(
                    this, context.Attachments, currentUser, timeProvider, context.Limits).Handle(command, cancellationToken);

                return (TResponse)(object)note;
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

/// <summary>What <see cref="ModulesSender"/> needs to route the Context requests that other modules send.</summary>
public sealed record ContextRoutes(
    InMemoryAttachmentRepository Attachments,
    InMemoryFileStorage Storage,
    ContextLimits Limits,
    ILoggerFactory Loggers);
