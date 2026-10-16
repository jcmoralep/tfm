using RutaProducto.Application.Features.Assistant.GetConversationAnswers;
using RutaProducto.Application.Features.Context;
using RutaProducto.Application.Features.Context.AddFileAttachment;
using RutaProducto.Application.Features.Context.AddLinkAttachment;
using RutaProducto.Application.Features.Context.AddNoteAttachment;
using RutaProducto.Application.Features.Context.DeleteAttachment;
using RutaProducto.Application.Features.Context.GetAttachmentDownload;
using RutaProducto.Application.Features.Context.GetContext;
using RutaProducto.Application.Features.Context.GetContextForAssistant;
using RutaProducto.Application.Features.Initiatives.GetInitiative;
using RutaProducto.Application.Features.Initiatives.MarkReadyToBuild;
using RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;
using RutaProducto.Application.Features.Initiatives.ReopenPlanning;
using RutaProducto.Application.Features.Initiatives.SetInitiativeDepth;
using RutaProducto.Application.Features.Initiatives.StartPlanning;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Tests.TestDoubles;

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
            case GetContextForAssistantQuery query:
                // Without context routes the initiative simply has no attachments.
                IReadOnlyList<ContextExcerpt> excerpts = context is null
                    ? []
                    : await new GetContextForAssistantQueryHandler(
                        this, context.Attachments, context.Storage, currentUser,
                        context.Loggers.CreateLogger<GetContextForAssistantQueryHandler>()).Handle(query, cancellationToken);

                return (TResponse)excerpts;
            case AddFileAttachmentCommand command when context is not null:
                var file = await new AddFileAttachmentCommandHandler(
                    this, context.Attachments, context.Storage, currentUser, timeProvider, context.Limits,
                    context.Loggers.CreateLogger<AddFileAttachmentCommandHandler>()).Handle(command, cancellationToken);

                return (TResponse)(object)file;
            case AddLinkAttachmentCommand command when context is not null:
                var link = await new AddLinkAttachmentCommandHandler(
                    this, context.Attachments, currentUser, timeProvider, context.Limits,
                    context.Loggers.CreateLogger<AddLinkAttachmentCommandHandler>()).Handle(command, cancellationToken);

                return (TResponse)(object)link;
            case AddNoteAttachmentCommand command when context is not null:
                var note = await new AddNoteAttachmentCommandHandler(
                    this, context.Attachments, currentUser, timeProvider, context.Limits,
                    context.Loggers.CreateLogger<AddNoteAttachmentCommandHandler>()).Handle(command, cancellationToken);

                return (TResponse)(object)note;
            case DeleteAttachmentCommand command when context is not null:
                var deleted = await new DeleteAttachmentCommandHandler(
                    this, context.Attachments, context.Storage, currentUser, context.Limits,
                    context.Loggers.CreateLogger<DeleteAttachmentCommandHandler>()).Handle(command, cancellationToken);

                return (TResponse)(object)deleted;
            case GetAttachmentDownloadQuery query when context is not null:
                var download = await new GetAttachmentDownloadQueryHandler(
                    this, context.Attachments, context.Storage, currentUser,
                    context.Loggers.CreateLogger<GetAttachmentDownloadQueryHandler>()).Handle(query, cancellationToken);

                return (TResponse)(object?)download!;
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
            case RecordInitiativeActivityCommand command:
                await new RecordInitiativeActivityCommandHandler(initiatives, currentUser, timeProvider).Handle(command, cancellationToken);
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
