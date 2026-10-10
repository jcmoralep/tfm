using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

/// <summary>
/// The conversation does not know about documents: reading its answers for drafting, or holding a generated
/// document aside, changes neither the script nor the transitions (RF-45, RF-53, RF-58).
/// </summary>
public sealed class ConversationDocumentsTests
{
    private readonly AssistantTestContext context = new();

    [Fact]
    public async Task Progress_and_next_topic_are_equal_with_and_without_documents()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerAll(id, confirm: false);
        var without = await context.Current(id);

        var brief = Artifact.Create(id, AssistantTestContext.UserA, ArtifactType.Brief, "# Brief", context.Clock.GetUtcNow());
        await context.Answers(id);
        var with = await context.Current(id);

        Assert.Equal(1, brief.Version);
        Assert.Equal(without.Journey.ProgressText, with.Journey.ProgressText);
        Assert.Equal(without.Journey.NextTopic!.Key, with.Journey.NextTopic!.Key);
        Assert.Equal(without.Journey.Step, with.Journey.Step);
        Assert.Equal(without.QuickReplies.Select(r => r.Key), with.QuickReplies.Select(r => r.Key));
    }

    [Fact]
    public async Task A_brief_generated_before_confirming_does_not_block_the_confirmation()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.AnswerAll(id, confirm: false);
        Assert.True((await context.Answers(id))!.ClarifyComplete);
        var brief = Artifact.Create(id, AssistantTestContext.UserA, ArtifactType.Brief, "# Brief", context.Clock.GetUtcNow());

        await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);

        Assert.Equal(InitiativeStatus.Planning, (await context.Details(id)).Status);
        Assert.Equal("# Brief", brief.Content);
        Assert.Equal(1, brief.Version);
        Assert.Equal(AssistantScript.Keys.Capabilities, (await context.Current(id)).Journey.NextTopic!.Key);
    }

    [Fact]
    public async Task Planificar_complete_points_to_the_documents_and_generates_nothing()
    {
        var id = await context.Initiatives.CreateClarifying("App");

        var view = await context.AnswerAll(id);

        Assert.Equal(AssistantScript.Keys.Closing, view.Journey.NextTopic!.Key);
        Assert.Equal(InitiativeStatus.Planning, (await context.Details(id)).Status);
        Assert.Contains("generar los documentos", AssistantScript.ClosingPrompt(InitiativeDepth.Standard), StringComparison.Ordinal);
        Assert.False(view.CanSend);
    }
}
