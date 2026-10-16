using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Features.Artifacts.ApproveArtifact;
using RutaProducto.Application.Features.Artifacts.AssignQuestionOwners;
using RutaProducto.Application.Features.Artifacts.GenerateArtifact;
using RutaProducto.Application.Features.Artifacts.GetArtifact;
using RutaProducto.Application.Features.Artifacts.GetArtifactsOverview;
using RutaProducto.Application.Features.Artifacts.ExportArtifact;
using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Application.Features.Artifacts.RegenerateArtifact;
using RutaProducto.Application.Features.Artifacts.SaveArtifactContent;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Application.Tests.Features.Assistant;
using RutaProducto.Application.Tests.Features.Initiatives;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Initiatives;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Tests.Features.Artifacts;

/// <summary>
/// Wires the real artifact handlers to the in-memory doubles. The initiative and conversation handlers underneath are
/// the real ones too (through <see cref="ModulesSender"/>), sharing the repositories of <see cref="Assistant"/>, so the
/// answers, the flags and the initiative status come from the real modules.
/// </summary>
public sealed class ArtifactTestContext
{
    public ArtifactTestContext()
    {
        Sender = new ModulesSender(Initiatives.Repository, Assistant.Conversations, User, Clock, Assistant.ContextRoutes);
    }

    public AssistantTestContext Assistant { get; } = new();

    public InitiativeTestContext Initiatives => Assistant.Initiatives;

    public InMemoryArtifactRepository Artifacts { get; } = new();

    public ScriptedArtifactDrafter Drafter { get; } = new();

    public ModulesSender Sender { get; }

    public FakeCurrentUser User => Initiatives.User;

    public FixedTimeProvider Clock => Initiatives.Clock;

    private ILogger<T> Log<T>() => Assistant.ContextRoutes.Loggers.CreateLogger<T>();

    public Task<ArtifactView> Generate(Guid id, ArtifactType type) =>
        new GenerateArtifactCommandHandler(Sender, Artifacts, Drafter, User, Clock, Log<GenerateArtifactCommandHandler>())
            .Handle(new GenerateArtifactCommand(id, type), default);

    public Task<ArtifactView> Save(Guid id, ArtifactType type, int expectedVersion, string content) =>
        new SaveArtifactContentCommandHandler(Sender, Artifacts, User, Clock, Log<SaveArtifactContentCommandHandler>())
            .Handle(new SaveArtifactContentCommand(id, type, expectedVersion, content), default);

    public Task<ArtifactView> Regenerate(Guid id, ArtifactType type, int expectedVersion, bool confirmed = true) =>
        new RegenerateArtifactCommandHandler(Sender, Artifacts, Drafter, User, Clock, Log<RegenerateArtifactCommandHandler>())
            .Handle(new RegenerateArtifactCommand(id, type, expectedVersion, confirmed), default);

    public Task<ArtifactView> Approve(Guid id, ArtifactType type, int expectedVersion) =>
        new ApproveArtifactCommandHandler(Sender, Artifacts, User, Clock, Log<ApproveArtifactCommandHandler>())
            .Handle(new ApproveArtifactCommand(id, type, expectedVersion), default);

    public Task<ArtifactView> Assign(Guid id, ArtifactType type, int expectedVersion, params QuestionAssignment[] assignments) =>
        new AssignQuestionOwnersCommandHandler(Sender, Artifacts, User, Clock, Log<AssignQuestionOwnersCommandHandler>())
            .Handle(new AssignQuestionOwnersCommand(id, type, expectedVersion, assignments), default);

    public Task<ArtifactView?> Get(Guid id, ArtifactType type) =>
        new GetArtifactQueryHandler(Sender, Artifacts, User).Handle(new GetArtifactQuery(id, type), default);

    public Task<ArtifactExport?> Export(Guid id, ArtifactType type) =>
        new ExportArtifactQueryHandler(Sender, Artifacts, User, new EchoExporter()).Handle(new ExportArtifactQuery(id, type), default);

    public Task<ArtifactsOverview?> Overview(Guid id) =>
        new GetArtifactsOverviewQueryHandler(Sender, Artifacts, User).Handle(new GetArtifactsOverviewQuery(id), default);

    /// <summary>A Clarifying Standard initiative whose five Aclarar topics are answered; the confirmation is still pending.</summary>
    public async Task<Guid> ClarifiedStandard(string name = "App")
    {
        var id = await Initiatives.CreateClarifying(name);
        await Assistant.AnswerAll(id, confirm: false);

        return id;
    }

    /// <summary>A Planning initiative of the level with every topic answered, so every document of the level is available.</summary>
    public async Task<Guid> AtClosing(InitiativeDepth depth, string name = "App")
    {
        var id = await Initiatives.CreateClarifying(name, depth);
        await Assistant.AnswerAll(id);

        return id;
    }

    private static readonly ArtifactType[] LargeChain = [ArtifactType.Prd, ArtifactType.Architecture, ArtifactType.EpicsAndStories];

    /// <summary>A Large initiative at the closing whose documents of the chain are generated and approved in order, up to and including <paramref name="last"/>.</summary>
    public async Task<Guid> LargeApprovedThrough(ArtifactType last)
    {
        var id = await AtClosing(InitiativeDepth.Large);

        foreach (var type in LargeChain.Take(Array.IndexOf(LargeChain, last) + 1))
        {
            await Generate(id, type);
            await Approve(id, type, 1);
        }

        return id;
    }

    public async Task<InitiativeStatus> StatusOf(Guid id) => (await Assistant.Details(id)).Status;

    public Artifact StoredArtifact(Guid id, ArtifactType type) => Artifacts.Stored.Single(a => a.InitiativeId == id && a.Type == type);
}

/// <summary>Stands in for the Markdown exporter (it lives in Infrastructure): echoes what the handler passed it.</summary>
internal sealed class EchoExporter : IArtifactExporter
{
    public ArtifactExport Export(ArtifactExportSource source) =>
        new($"{source.Type}-v{source.Version}.md", "text/markdown", $"{source.InitiativeName}|{source.Status}|{source.Content}");
}
