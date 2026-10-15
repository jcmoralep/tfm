using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Application.Features.Initiatives.ReopenPlanning;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Tests.Features.Artifacts;

public sealed class RegenerateArtifactTests
{
    private readonly ArtifactTestContext context = new();

    private async Task<Guid> ReadySpec()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Approve(id, ArtifactType.Spec, 1);

        return id;
    }

    [Fact]
    public async Task Regenerating_replaces_manual_edits_with_a_fresh_draft_numbered_from_one()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Save(id, ArtifactType.Spec, 1, "- **FR-4**: a mano\n- **FR-7**: otra");

        var view = await context.Regenerate(id, ArtifactType.Spec, 2);

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((3, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Equal(ScriptedArtifactDrafter.Draft(context.Drafter.Requests[^1]), stored.Content);
        Assert.DoesNotContain("FR-7", stored.Content, StringComparison.Ordinal);
        Assert.Equal(3, view.Version);
        Assert.Equal(2, context.Drafter.Requests.Count);
    }

    [Fact]
    public async Task Without_confirmation_nothing_happens()
    {
        var id = await ReadySpec();

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Spec, 1, confirmed: false));

        Assert.Equal("Confirme que desea reemplazar el documento.", error.Message);
        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((1, ArtifactStatus.Approved), (stored.Version, stored.Status));
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.Single(context.Drafter.Requests);
    }

    [Fact]
    public async Task Regenerating_an_approved_document_of_a_ready_initiative_returns_both_to_draft_and_planning()
    {
        var id = await ReadySpec();
        InitiativeStatus? statusAtSave = null;
        context.Artifacts.BeforeNextSave = async () => statusAtSave = await context.StatusOf(id);

        await context.Regenerate(id, ArtifactType.Spec, 1);

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((2, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Null(stored.ApprovedAt);
        Assert.Equal(InitiativeStatus.Planning, statusAtSave);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
    }

    [Fact]
    public async Task A_draft_that_fails_or_breaks_the_rules_leaves_the_ready_initiative_untouched()
    {
        var id = await ReadySpec();
        context.Drafter.FailuresRemaining = 1;
        context.Drafter.Failure = new AssistantUnavailableException();
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Regenerate(id, ArtifactType.Spec, 1));
        context.Drafter.Output = "   ";
        await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Spec, 1));

        Assert.Equal(ArtifactStatus.Approved, context.StoredArtifact(id, ArtifactType.Spec).Status);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.DoesNotContain(context.Sender.Sent, request => request is ReopenPlanningCommand);
    }

    [Theory]
    [InlineData(AssistantUnavailableReason.Quota)]
    [InlineData(AssistantUnavailableReason.Timeout)]
    [InlineData(AssistantUnavailableReason.Blocked)]
    [InlineData(AssistantUnavailableReason.Malformed)]
    public async Task A_failed_regeneration_with_a_reason_changes_nothing_and_returns_the_document_message(AssistantUnavailableReason reason)
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        await context.Generate(id, ArtifactType.Prd);
        await context.Save(id, ArtifactType.Prd, 1, "# PRD editado");
        await context.Approve(id, ArtifactType.Prd, 2);
        context.Drafter.FailuresRemaining = 1;
        context.Drafter.Failure = new AssistantUnavailableException(ArtifactTexts.DraftUnavailableFor(reason), reason);

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Regenerate(id, ArtifactType.Prd, 2));

        var stored = context.StoredArtifact(id, ArtifactType.Prd);
        Assert.Equal(ArtifactTexts.DraftUnavailableFor(reason), error.Message);
        Assert.Equal((2, ArtifactStatus.Approved, "# PRD editado"), (stored.Version, stored.Status, stored.Content));
    }

    [Fact]
    public async Task Regenerating_the_prd_uses_the_brief_as_approved_at_that_moment()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        await context.Generate(id, ArtifactType.Brief);
        await context.Approve(id, ArtifactType.Brief, 1);
        await context.Generate(id, ArtifactType.Prd);
        var first = context.Drafter.Requests[^1].BriefMarkdown;

        await context.Save(id, ArtifactType.Brief, 1, "# Brief editado");
        await context.Regenerate(id, ArtifactType.Prd, 1);
        var whileDraft = context.Drafter.Requests[^1].BriefMarkdown;

        await context.Approve(id, ArtifactType.Brief, 2);
        await context.Regenerate(id, ArtifactType.Prd, 2);

        Assert.NotNull(first);
        Assert.Null(whileDraft);
        Assert.Equal("# Brief editado", context.Drafter.Requests[^1].BriefMarkdown);
    }

    [Fact]
    public async Task Regenerating_a_document_not_generated_is_rejected()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Brief, 1));

        Assert.Equal("Todavía no se ha generado este documento.", error.Message);
        Assert.Empty(context.Artifacts.Stored);
    }

    [Fact]
    public async Task Regenerating_returns_every_question_to_the_default_and_the_gate_rejects_approval_again()
    {
        const string Draft = "# Spec\n\n- **Q-1**: uno (Responsable: por asignar; Revisar: por definir)";
        var id = await context.AtClosing(InitiativeDepth.Small);
        context.Drafter.Output = Draft;
        await context.Generate(id, ArtifactType.Spec);
        await context.Assign(id, ArtifactType.Spec, 1, new QuestionAssignment("Q-1", "Legal", "mañana"));
        Assert.Contains("Responsable: Legal", context.StoredArtifact(id, ArtifactType.Spec).Content, StringComparison.Ordinal);

        var view = await context.Regenerate(id, ArtifactType.Spec, 2);

        Assert.Equal(Draft, view.Content);
        Assert.Equal(["Q-1"], view.UnassignedQuestions);
        var error = await Assert.ThrowsAsync<DomainException>(() => context.Approve(id, ArtifactType.Spec, 3));
        Assert.Equal(ArtifactTexts.UnassignedQuestions(["Q-1"]), error.Message);
    }

    // Proves the handler passes the stored documents to the availability rule: without them the regeneration would go through.
    [Theory]
    [InlineData(ArtifactType.Architecture, ArtifactType.Prd, ArtifactTexts.WaitingForPrd)]
    [InlineData(ArtifactType.EpicsAndStories, ArtifactType.Prd, ArtifactTexts.WaitingForPrd)]
    [InlineData(ArtifactType.EpicsAndStories, ArtifactType.Architecture, ArtifactTexts.WaitingForArchitecture)]
    public async Task Regenerating_a_dependent_needs_its_upstream_approved(ArtifactType dependent, ArtifactType upstream, string message)
    {
        var id = await context.LargeApprovedThrough(ArtifactType.EpicsAndStories);
        await context.Save(id, upstream, 1, "# Editado\n\n- **FR-1**: cambiado");
        var before = context.StoredArtifact(id, dependent).Content;
        var requests = context.Drafter.Requests.Count;

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, dependent, 1));

        var stored = context.StoredArtifact(id, dependent);
        Assert.Equal(message, error.Message);
        Assert.Equal((1, ArtifactStatus.Approved, before), (stored.Version, stored.Status, stored.Content));
        Assert.Equal(requests, context.Drafter.Requests.Count);
    }

    private const string ArchitectureWithQuestion =
        "# Arquitectura\n\n## Preguntas para desarrollo\n\n- **Q-1**: ¿Qué tecnología usará la solución? (Responsable: Equipo de desarrollo; Revisar: antes de construir)";

    [Fact]
    public async Task Regenerating_the_architecture_returns_its_questions_to_the_pre_assigned_suffix()
    {
        var id = await context.LargeApprovedThrough(ArtifactType.Prd);
        context.Drafter.Output = ArchitectureWithQuestion;
        await context.Generate(id, ArtifactType.Architecture);
        await context.Assign(id, ArtifactType.Architecture, 1, new QuestionAssignment("Q-1", "TI", "esta semana"));
        Assert.Contains("Responsable: TI", context.StoredArtifact(id, ArtifactType.Architecture).Content, StringComparison.Ordinal);

        var view = await context.Regenerate(id, ArtifactType.Architecture, 2);

        Assert.EndsWith(OpenQuestions.DevelopmentSuffix, view.Content, StringComparison.Ordinal);
        Assert.Empty(view.UnassignedQuestions);
        Assert.Equal(3, view.Version);
    }

    [Fact]
    public async Task Regenerating_the_architecture_does_not_touch_the_epics()
    {
        var id = await context.LargeApprovedThrough(ArtifactType.Architecture);
        await context.Generate(id, ArtifactType.EpicsAndStories);
        var epics = context.StoredArtifact(id, ArtifactType.EpicsAndStories);
        var (version, status, content, updatedAt) = (epics.Version, epics.Status, epics.Content, epics.UpdatedAt);
        context.Clock.Advance(TimeSpan.FromMinutes(5));

        await context.Regenerate(id, ArtifactType.Architecture, 1);

        Assert.Equal((2, ArtifactStatus.Draft), (context.StoredArtifact(id, ArtifactType.Architecture).Version, context.StoredArtifact(id, ArtifactType.Architecture).Status));
        epics = context.StoredArtifact(id, ArtifactType.EpicsAndStories);
        Assert.Equal((version, status, content, updatedAt), (epics.Version, epics.Status, epics.Content, epics.UpdatedAt));
    }

    [Fact]
    public async Task A_stale_version_is_rejected_before_the_drafter_runs()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Save(id, ArtifactType.Spec, 1, "# Editada");

        var error = await Assert.ThrowsAsync<ConflictException>(() => context.Regenerate(id, ArtifactType.Spec, 1));

        Assert.Equal(ArtifactTexts.Conflict, error.Message);
        Assert.Equal("# Editada", context.StoredArtifact(id, ArtifactType.Spec).Content);
        Assert.Single(context.Drafter.Requests);
    }

    [Fact]
    public async Task A_document_whose_topics_are_no_longer_covered_cannot_be_regenerated()
    {
        var id = await context.ClarifiedStandard();
        await context.Generate(id, ArtifactType.Brief);
        var view = await context.Assistant.Current(id);
        view = await context.Assistant.Undo(id, view.Version);
        await context.Assistant.Undo(id, view.Version);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Brief, 1));

        Assert.Equal(ArtifactTexts.TopicsUncovered, error.Message);
        Assert.Equal(1, context.StoredArtifact(id, ArtifactType.Brief).Version);
    }
}
