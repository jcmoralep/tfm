using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Tests.Features.Artifacts;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Assistant;

/// <summary>
/// The conversation does not know about documents: reading its answers for drafting, or holding a generated
/// document aside, changes neither the script nor the transitions (RF-45, RF-53, RF-58).
/// </summary>
public sealed class ConversationDocumentsTests
{
    private readonly ArtifactTestContext artifacts = new();

    private AssistantTestContext context => artifacts.Assistant;

    [Fact]
    public async Task Progress_and_next_topic_are_equal_with_and_without_documents()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        await context.AnswerAll(id, confirm: false);
        var without = await context.Current(id);

        var brief = await artifacts.Generate(id, ArtifactType.Brief);
        var with = await context.Current(id);

        Assert.True(brief.Generated);
        Assert.Equal(without.Journey.ProgressText, with.Journey.ProgressText);
        Assert.Equal(without.Journey.NextTopic!.Key, with.Journey.NextTopic!.Key);
        Assert.Equal(without.Journey.Step, with.Journey.Step);
        Assert.Equal(without.QuickReplies.Select(r => r.Key), with.QuickReplies.Select(r => r.Key));
        Assert.Equal(without.Messages.Count, with.Messages.Count);
    }

    [Fact]
    public async Task A_brief_generated_before_confirming_does_not_block_the_confirmation()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.AnswerAll(id, confirm: false);
        Assert.True((await context.Answers(id))!.ClarifyComplete);
        await artifacts.Generate(id, ArtifactType.Brief);
        var before = artifacts.StoredArtifact(id, ArtifactType.Brief);

        await context.Send(id, view.Version, quickReplyKey: AssistantScript.ReplyKeys.ConfirmYes);

        var after = artifacts.StoredArtifact(id, ArtifactType.Brief);
        Assert.Equal(InitiativeStatus.Planning, (await context.Details(id)).Status);
        Assert.Equal((before.Content, before.Version), (after.Content, after.Version));
        Assert.Equal(AssistantScript.Keys.Capabilities, (await context.Current(id)).Journey.NextTopic!.Key);
    }

    [Fact]
    public async Task A_reopened_initiative_can_continue_the_conversation()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var closing = await context.AnswerAll(id);
        Assert.Equal(AssistantScript.Keys.Closing, closing.Journey.NextTopic!.Key);
        var (messages, calls) = (context.StoredMessages(id).Count, context.Assistant.Requests.Count);

        await context.Initiatives.MarkReadyToBuild(id);
        await Assert.ThrowsAsync<DomainException>(() => context.Start(id));
        await context.Initiatives.ReopenPlanning(id);
        var view = await context.Start(id);

        Assert.Equal(InitiativeStatus.Planning, (await context.Details(id)).Status);
        Assert.Equal(AssistantScript.Keys.Closing, view.Journey.NextTopic!.Key);
        Assert.False(view.CanSend);
        Assert.False(view.ReadOnly);
        Assert.False(view.NeedsResume);
        Assert.Equal((messages, calls), (context.StoredMessages(id).Count, context.Assistant.Requests.Count));

        var undone = await context.Undo(id, view.Version);

        Assert.Equal(AssistantScript.Keys.Priorities, undone.Journey.NextTopic!.Key);
        Assert.True(undone.CanSend);
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
