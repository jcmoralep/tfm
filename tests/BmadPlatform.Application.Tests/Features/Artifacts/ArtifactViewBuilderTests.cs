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

    private static ArtifactSummary Summary(ArtifactType type, ArtifactStatus status, int minutes = 0) =>
        new(type, status, 1, Now.AddMinutes(minutes));

    [Theory]
    [InlineData(ArtifactType.Architecture, 10, 0, 0, ArtifactType.Prd)]
    [InlineData(ArtifactType.Architecture, 0, 0, 0, null)]
    [InlineData(ArtifactType.Architecture, -10, 0, 0, null)]
    [InlineData(ArtifactType.EpicsAndStories, 0, 20, 10, ArtifactType.Architecture)]
    [InlineData(ArtifactType.EpicsAndStories, 0, 5, 10, null)]
    public void The_stale_upstream_hint_follows_the_timestamps(
        ArtifactType type, int prdMinutes, int architectureMinutes, int epicsMinutes, ArtifactType? expected)
    {
        var summaries = new List<ArtifactSummary>
        {
            Summary(ArtifactType.Prd, ArtifactStatus.Approved, prdMinutes),
            Summary(ArtifactType.Architecture, ArtifactStatus.Approved, type == ArtifactType.Architecture ? 0 : architectureMinutes),
        };

        if (type == ArtifactType.EpicsAndStories)
        {
            summaries.Add(Summary(type, ArtifactStatus.Draft, epicsMinutes));
        }

        var view = ArtifactViewBuilder.Build(
            Details(InitiativeStatus.Planning, InitiativeDepth.Large),
            new ConversationAnswers(true, true, []),
            type,
            Artifact.Create(Guid.NewGuid(), "user-a", type, "# Documento", Now),
            summaries);

        Assert.Equal(expected, view.StaleUpstream);
    }

    [Fact]
    public void There_is_no_hint_when_the_dependent_does_not_exist()
    {
        var view = ArtifactViewBuilder.Build(
            Details(InitiativeStatus.Planning, InitiativeDepth.Large),
            new ConversationAnswers(true, true, []),
            ArtifactType.Architecture,
            null,
            [Summary(ArtifactType.Prd, ArtifactStatus.Approved, 10)]);

        Assert.Null(view.StaleUpstream);
    }

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
    public void Unassigned_questions_are_listed_and_block_the_approval_until_they_are_assigned()
    {
        ArtifactView View(string content) => ArtifactViewBuilder.Build(
            Details(InitiativeStatus.Planning, InitiativeDepth.Small),
            new ConversationAnswers(true, true, []),
            ArtifactType.Spec,
            Artifact.Create(Guid.NewGuid(), "user-a", ArtifactType.Spec, content, Now),
            [Summary(ArtifactType.Spec, ArtifactStatus.Draft)]);

        var open = View("- **Q-1**: falta (Responsable: por asignar; Revisar: por definir)\n- **Q-2**: otra (Responsable: TI; Revisar: mañana)");
        var assigned = View("- **Q-1**: falta (Responsable: TI; Revisar: mañana)");

        Assert.Equal(["Q-1"], open.UnassignedQuestions);
        Assert.Equal(["Q-1", "Q-2"], open.Questions.Select(q => q.Id));
        Assert.False(open.CanApprove);
        Assert.False(open.ApprovalCompletesSet);
        Assert.Empty(assigned.UnassignedQuestions);
        Assert.True(assigned.CanApprove);
        Assert.True(assigned.ApprovalCompletesSet);
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
