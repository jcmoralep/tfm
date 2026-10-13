using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Artifacts.ApproveArtifact;
using BmadPlatform.Application.Features.Artifacts.AssignQuestionOwners;
using BmadPlatform.Application.Features.Artifacts.GenerateArtifact;
using BmadPlatform.Application.Features.Artifacts.GetArtifact;
using BmadPlatform.Application.Features.Artifacts.GetArtifactsOverview;
using BmadPlatform.Application.Features.Artifacts.ExportArtifact;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Artifacts.RegenerateArtifact;
using BmadPlatform.Application.Features.Artifacts.SaveArtifactContent;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Application.Tests.Features.Assistant;
using BmadPlatform.Application.Tests.Features.Initiatives;
using BmadPlatform.Application.Tests.TestDoubles;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

/// <summary>
/// Wires the real artifact handlers to the in-memory doubles. The initiative and conversation handlers underneath are
/// the real ones too (through <see cref="ModulesSender"/>), sharing the repositories of <see cref="Assistant"/>, so the
/// answers, the flags and the initiative status come from the real modules.
/// </summary>
public sealed class ArtifactTestContext
{
    public ArtifactTestContext()
    {
        Sender = new ModulesSender(Initiatives.Repository, Assistant.Conversations, User, Clock);
    }

    public AssistantTestContext Assistant { get; } = new();

    public InitiativeTestContext Initiatives => Assistant.Initiatives;

    public InMemoryArtifactRepository Artifacts { get; } = new();

    public ScriptedArtifactDrafter Drafter { get; } = new();

    public ModulesSender Sender { get; }

    public FakeCurrentUser User => Initiatives.User;

    public FixedTimeProvider Clock => Initiatives.Clock;

    public Task<ArtifactView> Generate(Guid id, ArtifactType type) =>
        new GenerateArtifactCommandHandler(Sender, Artifacts, Drafter, User, Clock)
            .Handle(new GenerateArtifactCommand(id, type), default);

    public Task<ArtifactView> Save(Guid id, ArtifactType type, int expectedVersion, string content) =>
        new SaveArtifactContentCommandHandler(Sender, Artifacts, User, Clock)
            .Handle(new SaveArtifactContentCommand(id, type, expectedVersion, content), default);

    public Task<ArtifactView> Regenerate(Guid id, ArtifactType type, int expectedVersion, bool confirmed = true) =>
        new RegenerateArtifactCommandHandler(Sender, Artifacts, Drafter, User, Clock)
            .Handle(new RegenerateArtifactCommand(id, type, expectedVersion, confirmed), default);

    public Task<ArtifactView> Approve(Guid id, ArtifactType type, int expectedVersion) =>
        new ApproveArtifactCommandHandler(Sender, Artifacts, User, Clock)
            .Handle(new ApproveArtifactCommand(id, type, expectedVersion), default);

    public Task<ArtifactView> Assign(Guid id, ArtifactType type, int expectedVersion, params QuestionAssignment[] assignments) =>
        new AssignQuestionOwnersCommandHandler(Sender, Artifacts, User, Clock)
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

    public async Task<InitiativeStatus> StatusOf(Guid id) => (await Assistant.Details(id)).Status;

    public Artifact StoredArtifact(Guid id, ArtifactType type) => Artifacts.Stored.Single(a => a.InitiativeId == id && a.Type == type);
}

/// <summary>Stands in for the Markdown exporter (it lives in Infrastructure): echoes what the handler passed it.</summary>
internal sealed class EchoExporter : IArtifactExporter
{
    public ArtifactExport Export(ArtifactExportSource source) =>
        new($"{source.Type}-v{source.Version}.md", "text/markdown", $"{source.InitiativeName}|{source.Status}|{source.Content}");
}
