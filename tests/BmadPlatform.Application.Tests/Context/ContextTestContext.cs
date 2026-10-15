using BmadPlatform.Application.Features.Context;
using BmadPlatform.Application.Features.Context.GetContext;
using BmadPlatform.Application.Tests.Features.Initiatives;
using BmadPlatform.Application.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.Context;

/// <summary>
/// Wires the real context handlers to the in-memory doubles. The initiative handlers underneath are the real ones
/// too (through <see cref="ModulesSender"/>). Switch user with <see cref="User"/>.
/// </summary>
public sealed class ContextTestContext
{
    public const string UserA = InitiativeTestContext.UserA;
    public const string UserB = InitiativeTestContext.UserB;

    public const long TenMegabytes = 10L * 1024 * 1024;

    public ContextTestContext(long maxFileBytes = TenMegabytes)
    {
        Limits = new ContextLimits(maxFileBytes);
        Attachments = new InMemoryAttachmentRepository(Journal);
        Storage = new InMemoryFileStorage(Journal);
        Loggers = LoggerFactory.Create(builder => builder.AddProvider(Logs));
        Sender = new ModulesSender(
            Initiatives.Repository,
            new InMemoryConversationRepository(),
            User,
            Clock,
            new ContextRoutes(Attachments, Storage, Limits, Loggers));
    }

    /// <summary>The order of storage and row operations, shared by both doubles.</summary>
    public List<string> Journal { get; } = [];

    public CapturingLoggerProvider Logs { get; } = new();

    public InitiativeTestContext Initiatives { get; } = new();

    public ContextLimits Limits { get; }

    public InMemoryAttachmentRepository Attachments { get; }

    public InMemoryFileStorage Storage { get; }

    public ILoggerFactory Loggers { get; }

    public ModulesSender Sender { get; }

    public FakeCurrentUser User => Initiatives.User;

    public FixedTimeProvider Clock => Initiatives.Clock;

    public Task<ContextView?> Get(Guid initiativeId) =>
        new GetContextQueryHandler(Sender, Attachments, User, Limits).Handle(new GetContextQuery(initiativeId), default);
}
