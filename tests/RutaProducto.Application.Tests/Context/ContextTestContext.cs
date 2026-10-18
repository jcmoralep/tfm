using RutaProducto.Application.Abstractions.Documents;
using RutaProducto.Application.Features.Context;
using RutaProducto.Application.Features.Context.AddFileAttachment;
using RutaProducto.Application.Features.Context.AddLinkAttachment;
using RutaProducto.Application.Features.Context.AddNoteAttachment;
using RutaProducto.Application.Features.Context.DeleteAttachment;
using RutaProducto.Application.Features.Context.GetAttachmentDownload;
using RutaProducto.Application.Features.Context.GetContext;
using RutaProducto.Application.Features.Context.GetContextForAssistant;
using RutaProducto.Application.Tests.Features.Initiatives;
using RutaProducto.Application.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Tests.Context;

/// <summary>
/// Wires the real context handlers to the in-memory doubles. The initiative handlers underneath are the real ones
/// too (through <see cref="ModulesSender"/>). Switch user with <see cref="User"/>.
/// </summary>
public sealed class ContextTestContext
{
    public const string UserA = InitiativeTestContext.UserA;
    public const string UserB = InitiativeTestContext.UserB;

    public const long TenMegabytes = 10L * 1024 * 1024;

    public ContextTestContext(long maxFileBytes = TenMegabytes, IReadOnlyList<IDocumentTextExtractor>? extractors = null, TimeSpan? extractionTimeout = null)
    {
        Limits = new ContextLimits(maxFileBytes) { ExtractionTimeout = extractionTimeout ?? ContextLimits.DefaultExtractionTimeout };
        Extractors = extractors ?? [];
        Attachments = new InMemoryAttachmentRepository(Journal);
        Storage = new InMemoryFileStorage(Journal);
        Loggers = LoggerFactory.Create(builder => builder.AddProvider(Logs));
        Sender = new ModulesSender(
            Initiatives.Repository,
            new InMemoryConversationRepository(),
            User,
            Clock,
            new ContextRoutes(Attachments, Storage, Limits, Loggers, Extractors));
    }

    /// <summary>The order of storage and row operations, shared by both doubles.</summary>
    public List<string> Journal { get; } = [];

    public CapturingLoggerProvider Logs { get; } = new();

    public InitiativeTestContext Initiatives { get; } = new();

    public ContextLimits Limits { get; }

    public IReadOnlyList<IDocumentTextExtractor> Extractors { get; }

    public InMemoryAttachmentRepository Attachments { get; }

    public InMemoryFileStorage Storage { get; }

    public ILoggerFactory Loggers { get; }

    public ModulesSender Sender { get; }

    public FakeCurrentUser User => Initiatives.User;

    public FixedTimeProvider Clock => Initiatives.Clock;

    public Task<ContextView?> Get(Guid initiativeId) =>
        new GetContextQueryHandler(Sender, Attachments, User, Limits).Handle(new GetContextQuery(initiativeId), default);

    /// <summary>Adds a file whose announced size defaults to the real one.</summary>
    public Task<ContextView> AddFile(Guid initiativeId, string name, byte[] bytes, long? declaredSize = null, string? contentType = "text/plain") =>
        new AddFileAttachmentCommandHandler(Sender, Attachments, Storage, Extractors, User, Clock, Limits, Loggers.CreateLogger<AddFileAttachmentCommandHandler>())
            .Handle(new AddFileAttachmentCommand(initiativeId, name, contentType, declaredSize ?? bytes.Length, new MemoryStream(bytes)), default);

    public Task<ContextView> AddLink(Guid initiativeId, string url) =>
        new AddLinkAttachmentCommandHandler(Sender, Attachments, User, Clock, Limits, Loggers.CreateLogger<AddLinkAttachmentCommandHandler>())
            .Handle(new AddLinkAttachmentCommand(initiativeId, url), default);

    public Task<ContextView> Delete(Guid initiativeId, Guid attachmentId) =>
        new DeleteAttachmentCommandHandler(Sender, Attachments, Storage, User, Limits, Loggers.CreateLogger<DeleteAttachmentCommandHandler>())
            .Handle(new DeleteAttachmentCommand(initiativeId, attachmentId), default);

    public Task<AttachmentDownload?> Download(Guid initiativeId, Guid attachmentId) =>
        new GetAttachmentDownloadQueryHandler(Sender, Attachments, Storage, User, Loggers.CreateLogger<GetAttachmentDownloadQueryHandler>())
            .Handle(new GetAttachmentDownloadQuery(initiativeId, attachmentId), default);

    public Task<IReadOnlyList<ContextExcerpt>> Excerpts(Guid initiativeId) =>
        new GetContextForAssistantQueryHandler(Sender, Attachments, Storage, User, Loggers.CreateLogger<GetContextForAssistantQueryHandler>())
            .Handle(new GetContextForAssistantQuery(initiativeId), default);

    public Task<ContextView> AddNote(Guid initiativeId, string title, string text) =>
        new AddNoteAttachmentCommandHandler(Sender, Attachments, User, Clock, Limits, Loggers.CreateLogger<AddNoteAttachmentCommandHandler>())
            .Handle(new AddNoteAttachmentCommand(initiativeId, title, text), default);
}
