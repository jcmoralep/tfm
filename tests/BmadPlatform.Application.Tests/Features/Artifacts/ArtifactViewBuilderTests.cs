using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

public sealed class ArtifactViewBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    public enum Doc
    {
        None,
        Draft,
        Approved,
    }

    private static InitiativeDetails Details(InitiativeStatus status, InitiativeDepth depth) =>
        new(Guid.NewGuid(), "App", null, status, DepthMode.Manual, depth, CreationStep.Details, Now, Now);

    private static Artifact? Stored(ArtifactType type, Doc doc)
    {
        if (doc == Doc.None)
        {
            return null;
        }

        var artifact = Artifact.Create(Guid.NewGuid(), "user-a", type, "# Documento", Now);

        if (doc == Doc.Approved)
        {
            artifact.Approve(Now);
        }

        return artifact;
    }

    private static ArtifactSummary Summary(ArtifactType type, ArtifactStatus status) => new(type, status, 1);

    [Theory]
    [InlineData(InitiativeStatus.Draft, InitiativeDepth.Small, ArtifactType.Spec, Doc.None, false, false, false, false, false, false)]
    [InlineData(InitiativeStatus.Planning, InitiativeDepth.Small, ArtifactType.Spec, Doc.None, true, true, true, false, false, false)]
    [InlineData(InitiativeStatus.Planning, InitiativeDepth.Small, ArtifactType.Spec, Doc.Draft, true, true, false, true, true, true)]
    [InlineData(InitiativeStatus.Planning, InitiativeDepth.Small, ArtifactType.Spec, Doc.Approved, true, true, false, true, true, false)]
    [InlineData(InitiativeStatus.Planning, InitiativeDepth.Small, ArtifactType.Spec, Doc.Draft, true, false, false, true, false, false)]
    [InlineData(InitiativeStatus.Clarifying, InitiativeDepth.Standard, ArtifactType.Brief, Doc.None, true, false, true, false, false, false)]
    [InlineData(InitiativeStatus.Clarifying, InitiativeDepth.Standard, ArtifactType.Prd, Doc.None, true, false, false, false, false, false)]
    [InlineData(InitiativeStatus.Clarifying, InitiativeDepth.Small, ArtifactType.Brief, Doc.Draft, true, true, false, false, false, false)]
    [InlineData(InitiativeStatus.ReadyToBuild, InitiativeDepth.Small, ArtifactType.Spec, Doc.Approved, true, true, false, true, true, false)]
    public void Action_flags_follow_the_initiative_state_and_the_document(
        InitiativeStatus status,
        InitiativeDepth depth,
        ArtifactType type,
        Doc doc,
        bool clarifyComplete,
        bool planComplete,
        bool canGenerate,
        bool canEdit,
        bool canRegenerate,
        bool canApprove)
    {
        var artifact = Stored(type, doc);
        var summaries = artifact is null ? [] : new[] { Summary(type, artifact.Status) };

        var view = ArtifactViewBuilder.Build(
            Details(status, depth),
            new ConversationAnswers(clarifyComplete, planComplete, []),
            type,
            artifact,
            summaries);

        Assert.Equal(canGenerate, view.CanGenerate);
        Assert.Equal(canEdit, view.CanEdit);
        Assert.Equal(canRegenerate, view.CanRegenerate);
        Assert.Equal(canApprove, view.CanApprove);
    }

    [Fact]
    public void A_document_outside_the_level_is_hidden_with_no_content()
    {
        var artifact = Stored(ArtifactType.Brief, Doc.Draft);

        var view = ArtifactViewBuilder.Build(
            Details(InitiativeStatus.Clarifying, InitiativeDepth.Small),
            new ConversationAnswers(true, false, []),
            ArtifactType.Brief,
            artifact,
            [Summary(ArtifactType.Brief, ArtifactStatus.Draft)]);

        Assert.False(view.Generated);
        Assert.Equal(0, view.Version);
        Assert.Equal(string.Empty, view.Content);
        Assert.Equal(ArtifactAvailability.NotRequired, view.Availability);
    }

    [Fact]
    public void A_document_not_generated_is_a_normal_result_without_content()
    {
        var view = ArtifactViewBuilder.Build(
            Details(InitiativeStatus.Planning, InitiativeDepth.Small),
            new ConversationAnswers(true, true, []),
            ArtifactType.Spec,
            null,
            []);

        Assert.False(view.Generated);
        Assert.Null(view.Status);
        Assert.Equal(0, view.Version);
        Assert.Empty(view.Warnings);
    }

    [Fact]
    public void Completion_flags_follow_the_required_set_and_the_initiative_status()
    {
        var planning = Details(InitiativeStatus.Planning, InitiativeDepth.Standard);
        var answers = new ConversationAnswers(true, true, []);
        var brief = Stored(ArtifactType.Brief, Doc.Approved);
        var prd = Stored(ArtifactType.Prd, Doc.Draft);

        // Approving the last draft of the set completes it.
        var last = ArtifactViewBuilder.Build(
            planning,
            answers,
            ArtifactType.Prd,
            prd,
            [Summary(ArtifactType.Brief, ArtifactStatus.Approved), Summary(ArtifactType.Prd, ArtifactStatus.Draft)]);

        Assert.True(last.ApprovalCompletesSet);
        Assert.False(last.CompletionPending);

        // Another draft is still open, so approving this one does not complete it.
        var first = ArtifactViewBuilder.Build(
            planning,
            answers,
            ArtifactType.Prd,
            prd,
            [Summary(ArtifactType.Brief, ArtifactStatus.Draft), Summary(ArtifactType.Prd, ArtifactStatus.Draft)]);

        Assert.False(first.ApprovalCompletesSet);

        // A fully approved set on a Planning initiative is the retry state after a failed ready transition.
        var pending = ArtifactViewBuilder.Build(
            planning,
            answers,
            ArtifactType.Brief,
            brief,
            [Summary(ArtifactType.Brief, ArtifactStatus.Approved), Summary(ArtifactType.Prd, ArtifactStatus.Approved)]);

        Assert.True(pending.CompletionPending);
        Assert.False(pending.ApprovalCompletesSet);

        // Large never completes: its later slots have no type.
        var large = ArtifactViewBuilder.Build(
            Details(InitiativeStatus.Planning, InitiativeDepth.Large),
            answers,
            ArtifactType.Prd,
            prd,
            [Summary(ArtifactType.Prd, ArtifactStatus.Draft)]);

        Assert.False(large.ApprovalCompletesSet);

        // Outside Planning there is nothing pending, even with everything approved.
        var ready = ArtifactViewBuilder.Build(
            Details(InitiativeStatus.ReadyToBuild, InitiativeDepth.Small),
            answers,
            ArtifactType.Spec,
            Stored(ArtifactType.Spec, Doc.Approved),
            [Summary(ArtifactType.Spec, ArtifactStatus.Approved)]);

        Assert.False(ready.CompletionPending);
    }
}
